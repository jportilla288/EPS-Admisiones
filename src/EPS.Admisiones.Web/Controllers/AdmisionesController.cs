using System.Text.Json;
using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Application.UseCases.AdmitirPaciente;
using EPS.Admisiones.Application.UseCases.ConsultarDetalleAdmision;
using Microsoft.AspNetCore.Mvc;

namespace EPS.Admisiones.Web.Controllers;

/// <summary>
/// Adaptador de entrada REST. Es una capa fina a proposito: traduce HTTP a
/// comando, invoca el puerto de entrada y traduce el resultado a HTTP.
/// Cero logica de negocio.
/// </summary>
[ApiController]
[Route("api/admisiones")]
[Produces("application/json")]
public sealed class AdmisionesController : ControllerBase
{
    private readonly IAdmitirPacienteUseCase _admitirPaciente;
    private readonly IConsultarDetalleAdmisionUseCase _consultarDetalle;
    private readonly IAdmisionesQuery _consultas;

    public AdmisionesController(
        IAdmitirPacienteUseCase admitirPaciente,
        IConsultarDetalleAdmisionUseCase consultarDetalle,
        IAdmisionesQuery consultas)
    {
        _admitirPaciente = admitirPaciente;
        _consultarDetalle = consultarDetalle;
        _consultas = consultas;
    }

    /// <summary>Admite un paciente a partir de su historia clinica FHIR/HL7.</summary>
    /// <param name="historiaClinica">Payload clinico completo, con datos anidados.</param>
    /// <param name="idempotencyKey">
    /// Cabecera opcional. Si el cliente reintenta por un timeout de red, la
    /// misma clave devuelve la admision original en lugar de duplicarla.
    /// </param>
    [HttpPost]
    [ProducesResponseType(typeof(AdmitirPacienteResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AdmitirPacienteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AdmitirAsync(
        [FromBody] JsonElement historiaClinica,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var resultado = await _admitirPaciente.EjecutarAsync(
            new AdmitirPacienteCommand(historiaClinica, idempotencyKey),
            cancellationToken);

        // 200 si era un reintento; 201 si realmente se creo el recurso.
        // Se usa Created con URI explicita y no CreatedAtAction: ASP.NET Core
        // recorta el sufijo "Async" de los nombres de accion, asi que
        // nameof(ObtenerDetalleAsync) no resolveria la ruta en tiempo de ejecucion.
        return resultado.EraDuplicado
            ? Ok(resultado)
            : Created($"/api/admisiones/{resultado.AdmisionId}", resultado);
    }

    /// <summary>
    /// Detalle completo de una admision: registro transaccional de SQL Server
    /// mas la historia clinica del almacen documental.
    /// </summary>
    /// <remarks>
    /// Si la historia clinica todavia no se ha propagado a MongoDB (o el
    /// almacen no responde), la respuesta llega igual con
    /// <c>historiaClinicaJson: null</c> y <c>tieneHistoriaClinica: false</c>.
    /// El dato transaccional nunca se retiene por un fallo del documental.
    /// </remarks>
    [HttpGet("{admisionId:guid}")]
    [ProducesResponseType(typeof(AdmisionDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdmisionDetalle>> ObtenerDetalleAsync(
        Guid admisionId,
        CancellationToken cancellationToken)
    {
        var detalle = await _consultarDetalle.EjecutarAsync(admisionId, cancellationToken);

        return detalle is null
            ? NotFound(new ProblemDetails
            {
                Title = "Admision no encontrada",
                Detail = $"No existe una admision con el identificador {admisionId}.",
                Status = StatusCodes.Status404NotFound
            })
            : Ok(detalle);
    }

    /// <summary>Ultimas admisiones registradas (hidrata el dashboard de auditoria).</summary>
    [HttpGet("recientes")]
    [ProducesResponseType(typeof(IReadOnlyList<AdmisionResumen>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdmisionResumen>>> ObtenerRecientesAsync(
        [FromQuery] int cantidad = 50,
        CancellationToken cancellationToken = default)
    {
        var admisiones = await _consultas.ObtenerRecientesAsync(cantidad, cancellationToken);

        return Ok(admisiones);
    }
}
