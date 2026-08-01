namespace EPS.Admisiones.Infrastructure.Outbox;

/// <summary>
/// Fila de la tabla Outbox. Es un detalle de INFRAESTRUCTURA, no de dominio:
/// por eso vive aqui y no en el proyecto Domain.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, string tipo, string payload, DateTime creadoEnUtc)
    {
        Id = id;
        Tipo = tipo;
        Payload = payload;
        CreadoEnUtc = creadoEnUtc;
        Intentos = 0;
    }

    public Guid Id { get; private set; }

    /// <summary>Nombre del contrato serializado, usado para deserializar al despachar.</summary>
    public string Tipo { get; private set; } = null!;

    /// <summary>Contrato serializado como JSON, incluido el payload FHIR completo.</summary>
    public string Payload { get; private set; } = null!;

    public DateTime CreadoEnUtc { get; private set; }

    /// <summary>Null mientras este pendiente. Es el indice por el que barre el despachador.</summary>
    public DateTime? ProcesadoEnUtc { get; private set; }

    public int Intentos { get; private set; }

    /// <summary>Backoff exponencial: no se reintenta antes de esta marca.</summary>
    public DateTime? DisponibleDesdeUtc { get; private set; }

    public string? UltimoError { get; private set; }

    public void MarcarProcesado(DateTime procesadoEnUtc)
    {
        ProcesadoEnUtc = procesadoEnUtc;
        UltimoError = null;
        DisponibleDesdeUtc = null;
    }

    public void MarcarFallo(string error, DateTime proximoIntentoUtc)
    {
        Intentos++;
        UltimoError = error.Length > 2000 ? error[..2000] : error;
        DisponibleDesdeUtc = proximoIntentoUtc;
    }
}
