namespace EPS.Admisiones.Infrastructure.Outbox;

/// <summary>Parametros de operacion del despachador. Se enlazan desde appsettings.</summary>
public sealed class OutboxOptions
{
    public const string SeccionConfiguracion = "Outbox";

    /// <summary>Cada cuanto barre la tabla en busca de mensajes pendientes.</summary>
    public TimeSpan IntervaloSondeo { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Mensajes por lote. Acota la memoria y la duracion de cada ciclo.</summary>
    public int TamanoLote { get; set; } = 20;

    /// <summary>
    /// Intentos antes de marcar la admision como FallidaSincronizacion y dejarla
    /// para conciliacion manual. Nunca se descarta el mensaje.
    /// </summary>
    public int MaximoIntentos { get; set; } = 5;

    /// <summary>Base del backoff exponencial entre reintentos de un mismo mensaje.</summary>
    public TimeSpan BackoffBase { get; set; } = TimeSpan.FromSeconds(5);
}
