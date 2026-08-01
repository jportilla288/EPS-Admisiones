using System.Text.Json;
using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Application.UseCases.AdmitirPaciente;
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
    private readonly IAdmisionesQuery _consultas;

    public AdmisionesController(
        IAdmitirPacienteUseCase admitirPaciente,
        IAdmisionesQuery consultas)
    {
        _admitirPaciente = admitirPaciente;
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
        // nameof(ObtenerRecientesAsync) no resolveria la ruta en tiempo de ejecucion.
        return resultado.EraDuplicado
            ? Ok(resultado)
            : Created("/api/admisiones/recientes?cantidad=1", resultado);
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
