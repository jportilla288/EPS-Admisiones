namespace EPS.Admisiones.Domain.Exceptions;

/// <summary>
/// Violacion de una invariante del dominio. Es un error del emisor (HTTP 400/422),
/// nunca un fallo tecnico. La capa Web la traduce a ProblemDetails.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
