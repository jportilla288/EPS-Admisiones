using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using Microsoft.AspNetCore.Mvc;

namespace EPS.Admisiones.Web.Controllers;

/// <summary>
/// Version corregida del endpoint de la Parte 4.
/// El analisis completo del codigo original esta en docs/code-review.md.
/// </summary>
[ApiController]
[Route("api/reportes")]
public sealed class ReportesController : ControllerBase
{
    private readonly IAdmisionesQuery _consultas;

    public ReportesController(IAdmisionesQuery consultas) => _consultas = consultas;

    /// <summary>
    /// Reporte mensual de atenciones por auditar.
    ///
    /// Cambios frente al original:
    ///  - Rango obligatorio: "mensual" ahora significa un mes, no la tabla entera.
    ///  - Filtrado y suma en el servidor de base de datos.
    ///  - Proyeccion directa a DTO, sin Include ni entidades rastreadas.
    ///  - IAsyncEnumerable: la respuesta se transmite por streaming, de modo que
    ///    la memoria del proceso no escala con el numero de filas.
    /// </summary>
    [HttpGet("mensual")]
    [ProducesResponseType(typeof(IAsyncEnumerable<ReporteAuditoriaItem>), StatusCodes.Status200OK)]
    public IAsyncEnumerable<ReporteAuditoriaItem> GenerarReporteMensual(
        [FromQuery] int anio,
        [FromQuery] int mes,
        CancellationToken cancellationToken)
    {
        if (anio is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(anio), "Anio fuera de rango.");
        }

        if (mes is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), "Mes fuera de rango.");
        }

        var desde = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var hasta = desde.AddMonths(1);

        // Se devuelve el IAsyncEnumerable sin materializar: ASP.NET Core lo
        // serializa elemento a elemento conforme llega desde SQL Server.
        return _consultas.ObtenerReporteAuditoriaAsync(desde, hasta, cancellationToken);
    }
}
