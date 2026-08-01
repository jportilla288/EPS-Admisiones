using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Repositories;

/// <summary>Adaptador de <see cref="IPacienteRepository"/> sobre EF Core.</summary>
public sealed class PacienteRepository : IPacienteRepository
{
    private readonly AdmisionesDbContext _db;

    public PacienteRepository(AdmisionesDbContext db) => _db = db;

    public Task<Paciente?> ObtenerPorDocumentoAsync(
        DocumentoPaciente documento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);

        // Se cargan las atenciones porque el agregado las necesita para aplicar
        // sus invariantes (idempotencia por AdmisionId). Es una lectura del lado
        // ESCRITURA: aqui el tracking si es necesario, a diferencia de las queries.
        return _db.Pacientes
            .Include(p => p.Atenciones)
            .FirstOrDefaultAsync(
                p => p.Documento.Tipo == documento.Tipo && p.Documento.Numero == documento.Numero,
                cancellationToken);
    }

    public async Task AgregarAsync(Paciente paciente, CancellationToken cancellationToken) =>
        await _db.Pacientes.AddAsync(paciente, cancellationToken);
}
