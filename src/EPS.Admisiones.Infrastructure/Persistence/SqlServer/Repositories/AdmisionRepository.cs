using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using Microsoft.EntityFrameworkCore;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Repositories;

/// <summary>Adaptador de <see cref="IAdmisionRepository"/> sobre EF Core.</summary>
public sealed class AdmisionRepository : IAdmisionRepository
{
    private readonly AdmisionesDbContext _db;

    public AdmisionRepository(AdmisionesDbContext db) => _db = db;

    public Task<Admision?> ObtenerAsync(Guid admisionId, CancellationToken cancellationToken) =>
        _db.Admisiones.FirstOrDefaultAsync(a => a.Id == admisionId, cancellationToken);

    public async Task AgregarAsync(Admision admision, CancellationToken cancellationToken) =>
        await _db.Admisiones.AddAsync(admision, cancellationToken);
}
