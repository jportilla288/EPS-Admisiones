using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Configurations;

public sealed class PacienteConfiguration : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("Pacientes");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Ignore(p => p.EventosDeDominio);

        builder.OwnsOne(p => p.Documento, documento =>
        {
            documento.Property(d => d.Tipo)
                .HasColumnName("TipoDocumento")
                .HasConversion<int>()
                .IsRequired();

            documento.Property(d => d.Numero)
                .HasColumnName("NumeroDocumento")
                .HasMaxLength(DocumentoPaciente.LongitudMaxima)
                .IsRequired();

            // Un afiliado no puede estar duplicado: la unicidad se garantiza en
            // la base de datos, no solo en codigo (dos instancias del App Service
            // pueden admitir al mismo paciente en paralelo).
            documento.HasIndex(d => new { d.Tipo, d.Numero })
                .IsUnique()
                .HasDatabaseName("UX_Pacientes_Documento");
        });

        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Apellido).HasMaxLength(100).IsRequired();

        builder.Property(p => p.Estado)
            .HasConversion<int>()
            .IsRequired();

        // La coleccion se expone como IReadOnlyCollection, asi que EF debe
        // escribir sobre el campo privado, no sobre la propiedad.
        builder.HasMany(p => p.Atenciones)
            .WithOne()
            .HasForeignKey(a => a.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Paciente.Atenciones))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
