using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Pacientes;
using EPS.Admisiones.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Unidad de trabajo sobre SQL Server. Un unico DbContext cubre las entidades
/// transaccionales y la tabla Outbox, que es justo lo que permite confirmarlas
/// en la misma transaccion sin recurrir a transacciones distribuidas (MSDTC),
/// no soportadas por Azure SQL.
/// </summary>
public sealed class AdmisionesDbContext : DbContext
{
    public AdmisionesDbContext(DbContextOptions<AdmisionesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Admision> Admisiones => Set<Admision>();

    public DbSet<Paciente> Pacientes => Set<Paciente>();

    public DbSet<Atencion> Atenciones => Set<Atencion>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("admisiones");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdmisionesDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
