using EPS.Admisiones.Application.Ports;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Adaptador de <see cref="IUnitOfWork"/>.
///
/// No abre una transaccion explicita a proposito: <c>SaveChangesAsync</c> ya
/// envuelve todos los cambios pendientes en UNA transaccion implicita. Abrirla
/// a mano aqui, ademas de redundante, romperia la estrategia de reintentos de
/// EF Core (<c>EnableRetryOnFailure</c>), que exige que las transacciones
/// explicitas se ejecuten dentro de una execution strategy.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AdmisionesDbContext _db;

    public UnitOfWork(AdmisionesDbContext db) => _db = db;

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
