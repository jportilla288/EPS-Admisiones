namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Proyeccion de solo lectura de una admision. La consume tanto el dashboard
/// de Blazor como la API. Es un DTO plano a proposito: nunca se exponen
/// entidades de dominio hacia afuera.
/// </summary>
public sealed record AdmisionResumen(
    Guid AdmisionId,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    decimal ValorCopago,
    string Moneda,
    DateTime FechaAdmisionUtc,
    string Estado);
