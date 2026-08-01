namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Indicadores operativos del modulo. Los consume el encabezado del dashboard
/// de auditoria. Son conteos agregados: nunca viajan datos clinicos.
/// </summary>
/// <param name="AdmisionesHoy">Admisiones registradas desde las 00:00 UTC.</param>
/// <param name="CopagoBloqueadoHoy">Suma de copagos bloqueados en el dia.</param>
/// <param name="PendientesSincronizacion">
/// Admisiones confirmadas en SQL cuya historia clinica aun no llega a MongoDB.
/// Es la medida directa de la ventana de consistencia eventual.
/// </param>
/// <param name="FallidasSincronizacion">
/// Admisiones que agotaron los reintentos del Outbox. Requieren conciliacion
/// manual: si este numero crece, hay un incidente.
/// </param>
public sealed record MetricasAdmisiones(
    int AdmisionesHoy,
    decimal CopagoBloqueadoHoy,
    int PendientesSincronizacion,
    int FallidasSincronizacion)
{
    public static MetricasAdmisiones Vacias() => new(0, 0m, 0, 0);
}
