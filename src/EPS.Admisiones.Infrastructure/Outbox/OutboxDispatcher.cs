using System.Text.Json;
using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Infrastructure.Persistence.SqlServer;
using EPS.Admisiones.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace EPS.Admisiones.Infrastructure.Outbox;

/// <summary>
/// Segunda mitad del patron Outbox: lee los mensajes confirmados en SQL Server
/// y los materializa en MongoDB con reintentos.
///
/// Garantia de entrega: AL MENOS UNA VEZ. Es seguro porque la escritura en
/// Mongo es un upsert por _id, es decir, idempotente.
///
/// Concurrencia entre instancias: con varias instancias del App Service, dos
/// despachadores pueden tomar el mismo lote. No corrompe datos (upsert
/// idempotente), solo genera trabajo repetido. Para produccion, el lote se
/// reclamaria con una sentencia
/// <c>UPDATE TOP (@n) ... WITH (READPAST, UPDLOCK) ... OUTPUT INSERTED.*</c>
/// o se alojaria el despachador en un unico worker (Azure Container App con
/// una replica, o Azure Functions con singleton).
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ResiliencePipelineProvider<string> _pipelines;
    private readonly OutboxOptions _opciones;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ResiliencePipelineProvider<string> pipelines,
        IOptions<OutboxOptions> opciones,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _pipelines = pipelines;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeline = _pipelines.GetPipeline(PoliticasResiliencia.PipelineMongo);
        using var temporizador = new PeriodicTimer(_opciones.IntervaloSondeo);

        _logger.LogInformation(
            "Despachador de Outbox iniciado (sondeo cada {Intervalo}).",
            _opciones.IntervaloSondeo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarLoteAsync(pipeline, stoppingToken);
                await temporizador.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // El bucle NUNCA debe morir: si lo hace, los mensajes dejan de
                // despacharse en silencio y la inconsistencia se vuelve permanente.
                _logger.LogError(ex, "Fallo un ciclo del despachador de Outbox.");
            }
        }

        _logger.LogInformation("Despachador de Outbox detenido.");
    }

    private async Task ProcesarLoteAsync(ResiliencePipeline pipeline, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var proveedor = scope.ServiceProvider;

        var db = proveedor.GetRequiredService<AdmisionesDbContext>();
        var historias = proveedor.GetRequiredService<IHistoriaClinicaRepository>();
        var notificador = proveedor.GetRequiredService<IAdmisionNotifier>();
        var ahora = proveedor.GetRequiredService<IRelojSistema>().UtcNow;

        var pendientes = await db.OutboxMessages
            .Where(m => m.ProcesadoEnUtc == null
                && (m.DisponibleDesdeUtc == null || m.DisponibleDesdeUtc <= ahora))
            .OrderBy(m => m.CreadoEnUtc)
            .Take(_opciones.TamanoLote)
            .ToListAsync(cancellationToken);

        if (pendientes.Count == 0)
        {
            return;
        }

        var notificaciones = new List<AdmisionResumen>(pendientes.Count);

        foreach (var mensaje in pendientes)
        {
            try
            {
                var resumen = await ProcesarMensajeAsync(
                    mensaje,
                    db,
                    historias,
                    pipeline,
                    ahora,
                    cancellationToken);

                if (resumen is not null)
                {
                    notificaciones.Add(resumen);
                }
            }
            catch (Exception ex)
            {
                // Un mensaje envenenado no puede frenar al resto del lote.
                await RegistrarFalloAsync(mensaje, db, ahora, ex, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Se notifica DESPUES del commit: auditoria nunca ve una admision que
        // todavia podria revertirse.
        foreach (var notificacion in notificaciones)
        {
            await notificador.NotificarAsync(notificacion, cancellationToken);
        }
    }

    private async Task<AdmisionResumen?> ProcesarMensajeAsync(
        OutboxMessage mensaje,
        AdmisionesDbContext db,
        IHistoriaClinicaRepository historias,
        ResiliencePipeline pipeline,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        if (mensaje.Tipo != typeof(SincronizarHistoriaClinica).FullName)
        {
            _logger.LogWarning(
                "Mensaje de Outbox {MensajeId} con tipo desconocido '{Tipo}'; se descarta.",
                mensaje.Id,
                mensaje.Tipo);

            mensaje.MarcarProcesado(ahora);
            return null;
        }

        var contrato = JsonSerializer.Deserialize<SincronizarHistoriaClinica>(
            mensaje.Payload,
            OutboxWriter.OpcionesJson)
            ?? throw new InvalidOperationException(
                $"El payload del mensaje {mensaje.Id} no pudo deserializarse.");

        var documento = DocumentoPaciente.Crear(contrato.TipoDocumento, contrato.NumeroDocumento);

        var historiaClinica = HistoriaClinica.Crear(
            contrato.HistoriaClinicaId,
            documento,
            contrato.RecursoFhir,
            contrato.ContenidoJson,
            contrato.CapturadaEnUtc);

        // Aqui vive la resiliencia: reintentos, timeout y circuit breaker.
        await pipeline.ExecuteAsync(
            async token => await historias.GuardarAsync(historiaClinica, token),
            cancellationToken);

        mensaje.MarcarProcesado(ahora);

        var admision = await db.Admisiones
            .FirstOrDefaultAsync(a => a.Id == contrato.AdmisionId, cancellationToken);

        admision?.ConfirmarSincronizacion(ahora);

        _logger.LogInformation(
            "Historia clinica {HistoriaClinicaId} sincronizada para la admision {AdmisionId}.",
            contrato.HistoriaClinicaId,
            contrato.AdmisionId);

        return new AdmisionResumen(
            contrato.AdmisionId,
            contrato.TipoDocumento,
            contrato.NumeroDocumento,
            contrato.ValorCopago,
            contrato.Moneda,
            contrato.CapturadaEnUtc,
            EstadoAdmision.Sincronizada.ToString());
    }

    private async Task RegistrarFalloAsync(
        OutboxMessage mensaje,
        AdmisionesDbContext db,
        DateTime ahora,
        Exception excepcion,
        CancellationToken cancellationToken)
    {
        // Backoff exponencial acotado: 5s, 10s, 20s, 40s...
        var exponente = Math.Min(mensaje.Intentos, 6);
        var espera = TimeSpan.FromTicks(_opciones.BackoffBase.Ticks * (long)Math.Pow(2, exponente));

        mensaje.MarcarFallo(excepcion.Message, ahora.Add(espera));

        var contrato = TryDeserializar(mensaje);

        if (contrato is not null)
        {
            var admision = await db.Admisiones
                .FirstOrDefaultAsync(a => a.Id == contrato.AdmisionId, cancellationToken);

            admision?.RegistrarFalloSincronizacion(excepcion.Message, _opciones.MaximoIntentos);
        }

        _logger.LogError(
            excepcion,
            "Fallo el mensaje de Outbox {MensajeId} (intento {Intento}). Proximo intento en {Espera}.",
            mensaje.Id,
            mensaje.Intentos,
            espera);
    }

    private static SincronizarHistoriaClinica? TryDeserializar(OutboxMessage mensaje)
    {
        try
        {
            return JsonSerializer.Deserialize<SincronizarHistoriaClinica>(
                mensaje.Payload,
                OutboxWriter.OpcionesJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
