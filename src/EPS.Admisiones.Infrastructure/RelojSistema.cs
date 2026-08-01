using EPS.Admisiones.Application.Ports;

namespace EPS.Admisiones.Infrastructure;

/// <summary>Reloj real. En los tests se sustituye por uno fijo.</summary>
public sealed class RelojSistema : IRelojSistema
{
    public DateTime UtcNow => DateTime.UtcNow;
}
