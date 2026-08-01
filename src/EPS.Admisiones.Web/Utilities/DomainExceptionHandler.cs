using EPS.Admisiones.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EPS.Admisiones.Web.Utilities;

/// <summary>
/// Traduce las excepciones de dominio a ProblemDetails (RFC 7807).
/// Una invariante violada es culpa del emisor (422), no un fallo del servidor:
/// devolver 500 en ese caso ensucia las alertas de produccion con ruido.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException dominio)
        {
            // Cualquier otra excepcion sigue el pipeline por defecto (500).
            return false;
        }

        _logger.LogWarning(dominio, "Solicitud rechazada por regla de dominio.");

        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "La solicitud no cumple una regla de negocio.",
            Detail = dominio.Message,
            Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problema.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problema, cancellationToken);

        return true;
    }
}
