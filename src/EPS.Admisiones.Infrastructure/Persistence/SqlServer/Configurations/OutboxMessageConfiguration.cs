using EPS.Admisiones.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Tipo).HasMaxLength(200).IsRequired();

        // El payload FHIR puede ser grande: nvarchar(max).
        builder.Property(m => m.Payload).IsRequired();

        builder.Property(m => m.CreadoEnUtc).IsRequired();
        builder.Property(m => m.UltimoError).HasMaxLength(2000);

        // Indice filtrado: el despachador barre CADA SEGUNDO, y con este filtro
        // el indice solo contiene los mensajes pendientes (unas pocas filas),
        // no el historico completo. Sin el, el barrido degrada con el volumen.
        builder.HasIndex(m => new { m.DisponibleDesdeUtc, m.CreadoEnUtc })
            .HasFilter("[ProcesadoEnUtc] IS NULL")
            .HasDatabaseName("IX_Outbox_Pendientes");
    }
}
