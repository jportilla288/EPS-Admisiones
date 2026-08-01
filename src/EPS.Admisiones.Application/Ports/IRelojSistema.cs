namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Abstraccion del reloj. Sin esto, cualquier assert sobre fechas en los tests
/// seria no determinista.
/// </summary>
public interface IRelojSistema
{
    DateTime UtcNow { get; }
}
