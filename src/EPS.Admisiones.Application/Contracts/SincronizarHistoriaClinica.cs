namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Mensaje de integracion que viaja por el Outbox. Lleva el payload FHIR
/// completo para que el despachador pueda materializar la historia clinica en
/// MongoDB sin volver a consultar a nadie.
/// </summary>
/// <param name="AdmisionId">Correlacion con el registro transaccional de SQL Server.</param>
/// <param name="HistoriaClinicaId">Clave de upsert en MongoDB (garantiza idempotencia).</param>
/// <param name="NombreCompleto">
/// Nombre del afiliado, viajando con el mensaje para que el despachador arme la
/// notificacion del dashboard sin un viaje extra a SQL Server por cada mensaje.
/// Es nullable a proposito: los mensajes encolados antes de que existiera este
/// campo se deserializan con null en lugar de reventar el despachador.
/// </param>
public sealed record SincronizarHistoriaClinica(
    Guid AdmisionId,
    string HistoriaClinicaId,
    string TipoDocumento,
    string NumeroDocumento,
    decimal ValorCopago,
    string Moneda,
    string RecursoFhir,
    string ContenidoJson,
    DateTime CapturadaEnUtc,
    string? NombreCompleto = null);
