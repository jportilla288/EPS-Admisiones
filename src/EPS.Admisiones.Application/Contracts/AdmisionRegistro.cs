namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Cara relacional de una admision: lo que vive en SQL Server. Es solo la
/// MITAD del detalle -- la historia clinica completa esta en el almacen
/// documental y la compone <c>ConsultarDetalleAdmisionUseCase</c>.
///
/// Se mantiene separado de <see cref="AdmisionDetalle"/> para que el puerto de
/// lectura sobre SQL no tenga que devolver un campo que no le corresponde
/// llenar.
/// </summary>
public sealed record AdmisionRegistro(
    Guid AdmisionId,
    string HistoriaClinicaId,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    decimal ValorCopago,
    string Moneda,
    DateTime FechaAdmisionUtc,
    string Estado,
    int IntentosSincronizacion,
    DateTime? SincronizadaEnUtc,
    string? MotivoFallo);
