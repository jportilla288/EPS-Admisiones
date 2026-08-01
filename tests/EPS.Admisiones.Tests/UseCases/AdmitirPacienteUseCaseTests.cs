using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.UseCases.AdmitirPaciente;
using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;
using EPS.Admisiones.Domain.Pacientes;
using EPS.Admisiones.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EPS.Admisiones.Tests.UseCases;

/// <summary>
/// Tests del caso de uso SIN bases de datos: solo dobles sobre los puertos.
/// Es la ganancia concreta de la arquitectura hexagonal en este proyecto.
/// </summary>
public sealed class AdmitirPacienteUseCaseTests
{
    private static readonly DateTime Ahora = new(2026, 7, 31, 14, 30, 0, DateTimeKind.Utc);

    private readonly BitacoraOperaciones _bitacora = new();
    private readonly AdmisionRepositoryFalso _admisiones;
    private readonly PacienteRepositoryFalso _pacientes;
    private readonly OutboxWriterFalso _outbox;
    private readonly UnitOfWorkFalso _unitOfWork;
    private readonly AdmitirPacienteUseCase _useCase;

    public AdmitirPacienteUseCaseTests()
    {
        _admisiones = new AdmisionRepositoryFalso(_bitacora);
        _pacientes = new PacienteRepositoryFalso(_bitacora);
        _outbox = new OutboxWriterFalso(_bitacora);
        _unitOfWork = new UnitOfWorkFalso(_bitacora);

        _useCase = new AdmitirPacienteUseCase(
            _admisiones,
            _pacientes,
            _outbox,
            _unitOfWork,
            new ExtractorDatosFacturables(),
            new RelojFijo(Ahora),
            NullLogger<AdmitirPacienteUseCase>.Instance);
    }

    [Fact]
    public async Task Admite_al_paciente_y_proyecta_documento_y_copago()
    {
        var comando = new AdmitirPacienteCommand(
            PayloadsDePrueba.HistoriaClinicaValida(numeroDocumento: "1098765432", copago: 25000m));

        var resultado = await _useCase.EjecutarAsync(comando, CancellationToken.None);

        var admision = Assert.Single(_admisiones.Agregadas);
        Assert.Equal(TipoDocumento.CC, admision.Documento.Tipo);
        Assert.Equal("1098765432", admision.Documento.Numero);
        Assert.Equal(25000m, admision.Copago.Monto);
        Assert.Equal("COP", admision.Copago.Moneda);
        Assert.Equal(Ahora, admision.FechaAdmisionUtc);
        Assert.Equal(EstadoAdmision.PendienteSincronizacion, admision.Estado);
        Assert.False(resultado.EraDuplicado);
    }

    [Fact]
    public async Task Encola_el_mensaje_de_Outbox_ANTES_del_commit()
    {
        // Esta es la invariante que sostiene toda la estrategia de dual write:
        // si el commit ocurriera primero, existiria una ventana en la que la
        // admision esta guardada pero nadie prometio sincronizar Mongo.
        await _useCase.EjecutarAsync(
            new AdmitirPacienteCommand(PayloadsDePrueba.HistoriaClinicaValida()),
            CancellationToken.None);

        var operaciones = _bitacora.Operaciones;
        var indiceEncolar = operaciones.ToList().IndexOf("EncolarOutbox");
        var indiceCommit = operaciones.ToList().IndexOf("Commit");

        Assert.True(indiceEncolar >= 0, "No se encolo ningun mensaje de Outbox.");
        Assert.True(indiceCommit >= 0, "Nunca se confirmo la unidad de trabajo.");
        Assert.True(indiceEncolar < indiceCommit, "El commit ocurrio antes de encolar el mensaje.");
        Assert.Equal(1, _unitOfWork.Confirmaciones);
    }

    [Fact]
    public async Task El_mensaje_de_Outbox_conserva_el_payload_clinico_completo()
    {
        await _useCase.EjecutarAsync(
            new AdmitirPacienteCommand(PayloadsDePrueba.HistoriaClinicaValida()),
            CancellationToken.None);

        var mensaje = Assert.IsType<SincronizarHistoriaClinica>(Assert.Single(_outbox.Encolados));

        Assert.Contains("diagnosticos", mensaje.ContenidoJson, StringComparison.Ordinal);
        Assert.Contains("J06.9", mensaje.ContenidoJson, StringComparison.Ordinal);
        Assert.Equal("Bundle", mensaje.RecursoFhir);
        Assert.Equal(_admisiones.Agregadas[0].Id, mensaje.AdmisionId);
        Assert.Equal(_admisiones.Agregadas[0].HistoriaClinicaId, mensaje.HistoriaClinicaId);
    }

    [Fact]
    public async Task Reintentar_con_la_misma_clave_de_idempotencia_no_duplica_la_admision()
    {
        var clave = Guid.NewGuid();
        var documento = DocumentoPaciente.Crear("CC", "1098765432");

        _admisiones.Existentes[clave] = Admision.Registrar(
            Guid.NewGuid(),
            documento,
            ValorCopago.Crear(25000m),
            Ahora,
            clave);

        var resultado = await _useCase.EjecutarAsync(
            new AdmitirPacienteCommand(PayloadsDePrueba.HistoriaClinicaValida(), clave),
            CancellationToken.None);

        Assert.True(resultado.EraDuplicado);
        Assert.Equal(clave, resultado.AdmisionId);
        Assert.Empty(_admisiones.Agregadas);
        Assert.Empty(_outbox.Encolados);
        Assert.Equal(0, _unitOfWork.Confirmaciones);
    }

    [Fact]
    public async Task Reutiliza_el_paciente_existente_en_lugar_de_duplicarlo()
    {
        var documento = DocumentoPaciente.Crear("CC", "1098765432");
        var existente = Paciente.Registrar(documento, "Ana", "Portilla");
        _pacientes.Existente = existente;

        await _useCase.EjecutarAsync(
            new AdmitirPacienteCommand(PayloadsDePrueba.HistoriaClinicaValida()),
            CancellationToken.None);

        Assert.Empty(_pacientes.Agregados);
        Assert.Single(existente.Atenciones);
        Assert.Equal(existente.Id, _admisiones.Agregadas[0].PacienteId);
    }

    [Fact]
    public async Task Un_payload_invalido_no_llega_a_tocar_la_base_de_datos()
    {
        var comando = new AdmitirPacienteCommand(
            PayloadsDePrueba.Json("""{ "resourceType": "Bundle", "copago": { "valor": 1000 } }"""));

        await Assert.ThrowsAsync<DomainException>(
            () => _useCase.EjecutarAsync(comando, CancellationToken.None));

        Assert.Empty(_admisiones.Agregadas);
        Assert.Empty(_outbox.Encolados);
        Assert.Equal(0, _unitOfWork.Confirmaciones);
    }

    [Fact]
    public async Task Si_SQL_falla_no_queda_nada_a_medias()
    {
        // Con el Outbox, un fallo de SQL aborta TODO: no hay documento huerfano
        // en Mongo porque Mongo aun no se ha tocado.
        _unitOfWork.ExcepcionAlGuardar = new InvalidOperationException("SQL Server no disponible.");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _useCase.EjecutarAsync(
                new AdmitirPacienteCommand(PayloadsDePrueba.HistoriaClinicaValida()),
                CancellationToken.None));

        Assert.Equal(0, _unitOfWork.Confirmaciones);
    }
}
