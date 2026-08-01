using EPS.Admisiones.Application.Contracts;

namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de lectura (lado query). Se separa del repositorio de escritura
/// porque las lecturas no necesitan agregados ni change tracking: proyectan
/// directamente a DTO. Es CQRS ligero, sin buses ni event sourcing.
/// </summary>
public interface IAdmisionesQuery
{
    /// <summary>Ultimas admisiones para hidratar el dashboard en el primer render.</summary>
    Task<IReadOnlyList<AdmisionResumen>> ObtenerRecientesAsync(
        int cantidad,
        CancellationToken cancellationToken);

    /// <summary>Indicadores operativos del encabezado del dashboard.</summary>
    Task<MetricasAdmisiones> ObtenerMetricasAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reporte mensual de auditoria (Parte 4). Devuelve IAsyncEnumerable para
    /// hacer streaming y no materializar todo el resultado en memoria.
    /// </summary>
    IAsyncEnumerable<ReporteAuditoriaItem> ObtenerReporteAuditoriaAsync(
        DateTime desdeUtc,
        DateTime hastaUtc,
        CancellationToken cancellationToken);
}
