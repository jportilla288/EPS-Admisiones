using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using Microsoft.Extensions.Logging;

namespace EPS.Admisiones.Application.UseCases.ConsultarDetalleAdmision;

/// <summary>
/// Lectura poliglota: compone el registro transaccional de SQL Server con la
/// historia clinica de MongoDB.
///
/// POR QUE ES UN CASO DE USO Y NO UN METODO MAS DE IAdmisionesQuery:
/// una consulta que cruza DOS almacenes no pertenece al puerto de ninguno de
/// los dos. Si viviera en IAdmisionesQuery, su adaptador de EF Core acabaria
/// dependiendo del driver de Mongo y la frontera del hexagono se rompe. La
/// composicion es una decision de aplicacion, y aqui es donde vive.
///
/// DEGRADACION PARCIAL: la historia clinica es opcional en la respuesta. Un
/// auditor que necesita verificar el copago bloqueado no puede quedarse sin
/// respuesta porque el almacen documental este caido -- el dato transaccional
/// es el que sostiene la operacion financiera y siempre esta en SQL Server.
/// </summary>
public sealed class ConsultarDetalleAdmisionUseCase : IConsultarDetalleAdmisionUseCase
{
    private readonly IAdmisionesQuery _consultas;
    private readonly IHistoriaClinicaRepository _historias;
    private readonly ILogger<ConsultarDetalleAdmisionUseCase> _logger;

    public ConsultarDetalleAdmisionUseCase(
        IAdmisionesQuery consultas,
        IHistoriaClinicaRepository historias,
        ILogger<ConsultarDetalleAdmisionUseCase> logger)
    {
        _consultas = consultas;
        _historias = historias;
        _logger = logger;
    }

    public async Task<AdmisionDetalle?> EjecutarAsync(
        Guid admisionId,
        CancellationToken cancellationToken)
    {
        if (admisionId == Guid.Empty)
        {
            return null;
        }

        // 1. Fuente de verdad transaccional. Si no esta aqui, no existe.
        var registro = await _consultas.ObtenerPorIdAsync(admisionId, cancellationToken);

        if (registro is null)
        {
            return null;
        }

        // 2. Almacen documental. Su ausencia degrada la respuesta, no la anula.
        var historiaClinicaJson = await LeerHistoriaClinicaAsync(
            registro.HistoriaClinicaId,
            cancellationToken);

        return AdmisionDetalle.Desde(registro, historiaClinicaJson);
    }

    private async Task<string?> LeerHistoriaClinicaAsync(
        string historiaClinicaId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _historias.ObtenerContenidoAsync(historiaClinicaId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancelacion del cliente: no es un fallo del almacen documental.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo leer la historia clinica {HistoriaClinicaId}; se devuelve "
                + "el registro transaccional sin ella.",
                historiaClinicaId);

            return null;
        }
    }
}
