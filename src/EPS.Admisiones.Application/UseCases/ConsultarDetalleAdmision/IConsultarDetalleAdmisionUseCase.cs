using EPS.Admisiones.Application.Contracts;

namespace EPS.Admisiones.Application.UseCases.ConsultarDetalleAdmision;

/// <summary>
/// Puerto de entrada para consultar una admision completa.
/// </summary>
public interface IConsultarDetalleAdmisionUseCase
{
    /// <summary>Devuelve <c>null</c> si la admision no existe.</summary>
    Task<AdmisionDetalle?> EjecutarAsync(Guid admisionId, CancellationToken cancellationToken);
}
