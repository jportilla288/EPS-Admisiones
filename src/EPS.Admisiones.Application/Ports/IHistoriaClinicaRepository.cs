using EPS.Admisiones.Domain.Admisiones;

namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de salida hacia el almacen documental (MongoDB / Azure Cosmos DB for MongoDB).
/// </summary>
public interface IHistoriaClinicaRepository
{
    /// <summary>
    /// Upsert por <see cref="HistoriaClinica.Id"/>. DEBE ser idempotente: el
    /// despachador del Outbox entrega al menos una vez, asi que reprocesar el
    /// mismo mensaje no puede duplicar documentos.
    /// </summary>
    Task GuardarAsync(HistoriaClinica historiaClinica, CancellationToken cancellationToken);

    Task<string?> ObtenerContenidoAsync(string historiaClinicaId, CancellationToken cancellationToken);
}
