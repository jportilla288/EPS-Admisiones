using EPS.Admisiones.Domain.Admisiones;

namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de salida hacia el almacen transaccional (SQL Server).
/// Deliberadamente NO expone IQueryable: si lo hiciera, filtrarian desde el
/// caso de uso y la capa Application terminaria acoplada a EF Core.
/// </summary>
public interface IAdmisionRepository
{
    Task<Admision?> ObtenerAsync(Guid admisionId, CancellationToken cancellationToken);

    /// <summary>
    /// Adjunta la admision a la unidad de trabajo. NO persiste: el commit
    /// ocurre en <see cref="IUnitOfWork.GuardarCambiosAsync"/>.
    /// </summary>
    Task AgregarAsync(Admision admision, CancellationToken cancellationToken);
}
