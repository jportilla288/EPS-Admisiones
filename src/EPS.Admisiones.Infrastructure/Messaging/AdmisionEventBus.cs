using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using Microsoft.Extensions.Logging;

namespace EPS.Admisiones.Infrastructure.Messaging;

/// <summary>
/// Bus en memoria entre el despachador del Outbox y los dashboards de Blazor
/// Server conectados. Se registra como SINGLETON: es el punto de encuentro
/// entre un BackgroundService y N circuitos de usuario.
///
/// Limitacion consciente y documentada: al ser en memoria, solo alcanza a los
/// circuitos alojados en ESTA instancia del App Service. Con varias instancias
/// hay que sustituirlo por un backplane (Azure SignalR Service o Redis) sin
/// tocar nada mas, porque el resto del sistema depende de IAdmisionNotifier,
/// no de esta clase.
/// </summary>
public sealed class AdmisionEventBus : IAdmisionNotifier
{
    private readonly ILogger<AdmisionEventBus> _logger;

    public AdmisionEventBus(ILogger<AdmisionEventBus> logger) => _logger = logger;

    /// <summary>
    /// Los componentes se suscriben aqui. CADA suscripcion obliga a una baja
    /// en Dispose: un singleton que retiene delegados de componentes ya
    /// destruidos es la fuga de memoria clasica de Blazor Server.
    /// </summary>
    public event Func<AdmisionResumen, Task>? AdmisionRegistrada;

    public async Task NotificarAsync(AdmisionResumen admision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admision);

        var suscriptores = AdmisionRegistrada;

        if (suscriptores is null)
        {
            return;
        }

        foreach (var suscriptor in suscriptores.GetInvocationList()
                     .Cast<Func<AdmisionResumen, Task>>())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await suscriptor(admision);
            }
            catch (Exception ex)
            {
                // Un dashboard que falla (p. ej. circuito ya cerrado) no puede
                // tumbar la notificacion de los demas ni al despachador.
                _logger.LogWarning(
                    ex,
                    "Un suscriptor fallo al recibir la admision {AdmisionId}.",
                    admision.AdmisionId);
            }
        }
    }
}
