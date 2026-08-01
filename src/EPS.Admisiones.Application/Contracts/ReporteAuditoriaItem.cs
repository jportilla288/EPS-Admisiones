namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Fila del reporte mensual de auditoria (Parte 4 de la prueba).
/// Es un DTO de proyeccion: EF Core lo materializa directamente desde SQL,
/// sin traer entidades ni activar el change tracker.
/// </summary>
public sealed record ReporteAuditoriaItem(string NombreCompleto, decimal TotalAuditar);
