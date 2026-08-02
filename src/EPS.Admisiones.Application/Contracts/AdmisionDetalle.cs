namespace EPS.Admisiones.Application.Contracts;

/// <summary>
/// Vista completa de una admision: combina el registro transaccional de SQL
/// Server con la historia clinica del almacen documental.
///
/// Es la unica proyeccion del sistema que LEE de las dos persistencias, y por
/// eso es la que demuestra el ciclo completo de la persistencia poliglota:
/// hasta ahora MongoDB solo se escribia.
/// </summary>
/// <param name="HistoriaClinicaJson">
/// Documento clinico integro tal como se recibio. Es <c>null</c> cuando el
/// Outbox todavia no lo ha propagado a MongoDB, o cuando el almacen documental
/// no responde: en ambos casos se prefiere devolver el registro transaccional
/// incompleto antes que fallar la consulta entera.
/// </param>
public sealed record AdmisionDetalle(
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
    string? MotivoFallo,
    string? HistoriaClinicaJson)
{
    /// <summary>
    /// Permite a la interfaz distinguir "todavia no llego" de "no se pudo leer"
    /// sin que el consumidor tenga que interpretar el estado por su cuenta.
    /// </summary>
    public bool TieneHistoriaClinica => HistoriaClinicaJson is not null;

    public static AdmisionDetalle Desde(AdmisionRegistro registro, string? historiaClinicaJson)
    {
        ArgumentNullException.ThrowIfNull(registro);

        return new AdmisionDetalle(
            registro.AdmisionId,
            registro.HistoriaClinicaId,
            registro.TipoDocumento,
            registro.NumeroDocumento,
            registro.NombreCompleto,
            registro.ValorCopago,
            registro.Moneda,
            registro.FechaAdmisionUtc,
            registro.Estado,
            registro.IntentosSincronizacion,
            registro.SincronizadaEnUtc,
            registro.MotivoFallo,
            historiaClinicaJson);
    }
}
