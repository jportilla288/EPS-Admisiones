using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Configurations;

/// <summary>
/// Mapeo de <see cref="Admision"/>. Se configura por Fluent API y no por
/// atributos, para que el dominio siga sin conocer EF Core.
/// </summary>
public sealed class AdmisionConfiguration : IEntityTypeConfiguration<Admision>
{
    public void Configure(EntityTypeBuilder<Admision> builder)
    {
        builder.ToTable("Admisiones");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Los eventos de dominio son estado en memoria: nunca se persisten.
        builder.Ignore(a => a.EventosDeDominio);

        builder.Property(a => a.PacienteId).IsRequired();

        builder.OwnsOne(a => a.Documento, documento =>
        {
            documento.Property(d => d.Tipo)
                .HasColumnName("TipoDocumento")
                .HasConversion<int>()
                .IsRequired();

            documento.Property(d => d.Numero)
                .HasColumnName("NumeroDocumento")
                .HasMaxLength(DocumentoPaciente.LongitudMaxima)
                .IsRequired();
        });

        builder.OwnsOne(a => a.Copago, copago =>
        {
            copago.Property(c => c.Monto)
                .HasColumnName("ValorCopago")
                .HasPrecision(18, 2)
                .IsRequired();

            copago.Property(c => c.Moneda)
                .HasColumnName("MonedaCopago")
                .HasMaxLength(3)
                .IsFixedLength()
                .IsRequired();
        });

        builder.Property(a => a.HistoriaClinicaId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.FechaAdmisionUtc).IsRequired();

        builder.Property(a => a.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.IntentosSincronizacion).IsRequired();
        builder.Property(a => a.MotivoFallo).HasMaxLength(2000);

        // Concurrencia optimista sin bloqueos: SQL Server genera el rowversion.
        builder.Property(a => a.RowVersion).IsRowVersion();

        // El dashboard consulta las N mas recientes: indice descendente por fecha.
        builder.HasIndex(a => a.FechaAdmisionUtc)
            .IsDescending()
            .HasDatabaseName("IX_Admisiones_FechaAdmisionUtc");

        // Filtrado de conciliacion: solo interesan las no sincronizadas.
        builder.HasIndex(a => a.Estado)
            .HasFilter("[Estado] <> 2")
            .HasDatabaseName("IX_Admisiones_Estado_Pendientes");

        builder.HasIndex(a => a.HistoriaClinicaId)
            .IsUnique()
            .HasDatabaseName("UX_Admisiones_HistoriaClinicaId");
    }
}
