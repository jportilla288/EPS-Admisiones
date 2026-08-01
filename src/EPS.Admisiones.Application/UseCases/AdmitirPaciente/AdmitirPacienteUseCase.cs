using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.Events;
using EPS.Admisiones.Domain.Pacientes;
using Microsoft.Extensions.Logging;

namespace EPS.Admisiones.Application.UseCases.AdmitirPaciente;

/// <summary>
/// Caso de uso central del modulo de admisiones.
///
/// ESTRATEGIA DE DUAL WRITE (decision arquitectonica clave de la prueba):
/// en lugar de escribir en MongoDB y despues en SQL Server -- dos escrituras
/// remotas sin transaccion comun, con una ventana real de inconsistencia --
/// se invierte el orden y se aplica el patron Outbox:
///
///   1. UNA sola transaccion en SQL Server escribe, atomicamente:
///        a) el paciente y su atencion facturable,
///        b) la fila de la admision con el copago bloqueado, y
///        c) el mensaje de Outbox con el payload FHIR completo.
///   2. Un despachador en segundo plano lee el Outbox y materializa la
///      historia clinica en MongoDB con upsert idempotente, con reintentos.
///
/// Consecuencia: el escenario "Mongo OK pero SQL falla" deja de existir por
/// construccion, porque ya no hay dos escrituras que puedan divergir. Si SQL
/// falla, no se admitio a nadie y el cliente recibe error. Si SQL tiene exito,
/// la admision esta garantizada y Mongo converge (consistencia eventual
/// acotada y observable via EstadoAdmision).
/// </summary>
public sealed class AdmitirPacienteUseCase : IAdmitirPacienteUseCase
{
    private readonly IAdmisionRepository _admisiones;
    private readonly IPacienteRepository _pacientes;
    private readonly IOutboxWriter _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExtractorDatosFacturables _extractor;
    private readonly IRelojSistema _reloj;
    private readonly ILogger<AdmitirPacienteUseCase> _logger;

    public AdmitirPacienteUseCase(
        IAdmisionRepository admisiones,
        IPacienteRepository pacientes,
        IOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        IExtractorDatosFacturables extractor,
        IRelojSistema reloj,
        ILogger<AdmitirPacienteUseCase> logger)
    {
        _admisiones = admisiones;
        _pacientes = pacientes;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _extractor = extractor;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<AdmitirPacienteResult> EjecutarAsync(
        AdmitirPacienteCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Idempotencia: si el cliente reintenta con la misma clave, no se
        //    duplica la admision ni se vuelve a bloquear el copago.
        if (command.AdmisionId is { } idSolicitado)
        {
            var existente = await _admisiones.ObtenerAsync(idSolicitado, cancellationToken);

            if (existente is not null)
            {
                _logger.LogInformation(
                    "Admision {AdmisionId} ya existia; se devuelve la respuesta original.",
                    idSolicitado);

                return Mapear(existente, eraDuplicado: true);
            }
        }

        // 2. El dominio valida y construye. Cualquier dato invalido revienta
        //    aqui, ANTES de tocar la base de datos.
        var datos = _extractor.Extraer(command.HistoriaClinica);
        var ahora = _reloj.UtcNow;

        var paciente = await _pacientes.ObtenerPorDocumentoAsync(datos.Documento, cancellationToken);

        if (paciente is null)
        {
            paciente = Paciente.Registrar(datos.Documento, datos.Nombre, datos.Apellido);
            await _pacientes.AgregarAsync(paciente, cancellationToken);
        }
        else
        {
            paciente.ActualizarNombre(datos.Nombre, datos.Apellido);
        }

        var admision = Admision.Registrar(
            paciente.Id,
            datos.Documento,
            datos.Copago,
            ahora,
            command.AdmisionId);

        paciente.RegistrarAtencion(admision.Id, datos.Copago, datos.RequiereAuditoria, ahora);

        await _admisiones.AgregarAsync(admision, cancellationToken);

        // 3. Los eventos de dominio se traducen a mensajes de Outbox y se
        //    adjuntan a la MISMA unidad de trabajo.
        foreach (var evento in admision.EventosDeDominio.OfType<PacienteAdmitido>())
        {
            await _outbox.EncolarAsync(
                new SincronizarHistoriaClinica(
                    evento.AdmisionId,
                    evento.HistoriaClinicaId,
                    evento.Documento.Tipo.ToString(),
                    evento.Documento.Numero,
                    evento.Copago.Monto,
                    evento.Copago.Moneda,
                    datos.RecursoFhir,
                    command.HistoriaClinica.GetRawText(),
                    evento.OcurridoEnUtc),
                cancellationToken);
        }

        // 4. Commit atomico: paciente + atencion + admision + mensaje de Outbox.
        await _unitOfWork.GuardarCambiosAsync(cancellationToken);
        admision.LimpiarEventos();

        _logger.LogInformation(
            "Admision {AdmisionId} registrada para el documento {TipoDocumento} en estado {Estado}.",
            admision.Id,
            admision.Documento.Tipo,
            admision.Estado);

        return Mapear(admision, eraDuplicado: false);
    }

    private static AdmitirPacienteResult Mapear(Admision admision, bool eraDuplicado) =>
        new(admision.Id,
            admision.HistoriaClinicaId,
            admision.Estado.ToString(),
            admision.FechaAdmisionUtc,
            eraDuplicado);
}
