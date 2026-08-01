namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Mensaje de integracion que viaja por el Outbox. Lleva el payload FHIR
/// completo para que el despachador pueda materializar la historia clinica en
/// MongoDB sin volver a consultar a nadie.
/// </summary>
/// <param name="AdmisionId">Correlacion con el registro transaccional de SQL Server.</param>
/// <param name="HistoriaClinicaId">Clave de upsert en MongoDB (garantiza idempotencia).</param>
public sealed record SincronizarHistoriaClinica(
    Guid AdmisionId,
    string HistoriaClinicaId,
    string TipoDocumento,
    string NumeroDocumento,
    decimal ValorCopago,
    string Moneda,
    string RecursoFhir,
    string ContenidoJson,
    DateTime CapturadaEnUtc);
