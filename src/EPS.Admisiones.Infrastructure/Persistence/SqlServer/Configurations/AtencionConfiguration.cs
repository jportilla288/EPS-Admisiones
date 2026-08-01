using EPS.Admisiones.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Configurations;

public sealed class AtencionConfiguration : IEntityTypeConfiguration<Atencion>
{
    public void Configure(EntityTypeBuilder<Atencion> builder)
    {
        builder.ToTable("Atenciones");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AdmisionId).IsRequired();

        builder.OwnsOne(a => a.Valor, valor =>
        {
            valor.Property(v => v.Monto)
                .HasColumnName("Valor")
                .HasPrecision(18, 2)
                .IsRequired();

            valor.Property(v => v.Moneda)
                .HasColumnName("Moneda")
                .HasMaxLength(3)
                .IsFixedLength()
                .IsRequired();
        });

        builder.Property(a => a.RequiereAuditoria).IsRequired();
        builder.Property(a => a.FechaUtc).IsRequired();

        // Indice de cobertura del reporte mensual (Parte 4): permite resolver
        // el filtro y la suma sin tocar la tabla base (index-only scan).
        builder.HasIndex(a => new { a.RequiereAuditoria, a.FechaUtc })
            .IncludeProperties(a => a.PacienteId)
            .HasDatabaseName("IX_Atenciones_Auditoria_Fecha");

        builder.HasIndex(a => a.AdmisionId)
            .IsUnique()
            .HasDatabaseName("UX_Atenciones_AdmisionId");
    }
}
