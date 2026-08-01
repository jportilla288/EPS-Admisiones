namespace EPS.Admisiones.Domain.Common;

/// <summary>
/// Marcador de evento de dominio. La entidad los acumula; el caso de uso los
/// vuelca al Outbox dentro de la misma transaccion SQL.
/// </summary>
public interface IDomainEvent
{
    Guid EventoId { get; }

    DateTime OcurridoEnUtc { get; }
}
