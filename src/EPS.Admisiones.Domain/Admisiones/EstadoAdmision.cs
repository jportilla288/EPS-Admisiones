namespace EPS.Admisiones.Domain.Admisiones;

/// <summary>
/// Ciclo de vida de la admision frente a la persistencia poliglota.
/// El estado es lo que hace observable (y auditable) la consistencia eventual
/// entre SQL Server y MongoDB.
/// </summary>
public enum EstadoAdmision
{
    /// <summary>
    /// Registrada en SQL Server con el copago bloqueado. La historia clinica
    /// todavia no se ha materializado en MongoDB.
    /// </summary>
    PendienteSincronizacion = 1,

    /// <summary>
    /// Historia clinica confirmada en MongoDB. Estado final feliz.
    /// </summary>
    Sincronizada = 2,

    /// <summary>
    /// El Outbox agoto los reintentos. Requiere intervencion manual;
    /// el copago sigue bloqueado y la admision NO se perdio.
    /// </summary>
    FallidaSincronizacion = 3
}
