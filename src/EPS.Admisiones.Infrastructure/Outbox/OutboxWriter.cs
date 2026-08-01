using System.Text.Json;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Infrastructure.Persistence.SqlServer;

namespace EPS.Admisiones.Infrastructure.Outbox;

/// <summary>
/// Adaptador de <see cref="IOutboxWriter"/>. Escribe sobre el MISMO DbContext
/// que los repositorios: esa es toda la magia del patron. El mensaje no se
/// persiste hasta que el UnitOfWork confirma.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    internal static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly AdmisionesDbContext _db;
    private readonly IRelojSistema _reloj;

    public OutboxWriter(AdmisionesDbContext db, IRelojSistema reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    public async Task EncolarAsync<TMensaje>(TMensaje mensaje, CancellationToken cancellationToken)
        where TMensaje : class
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var fila = new OutboxMessage(
            Guid.NewGuid(),
            typeof(TMensaje).FullName!,
            JsonSerializer.Serialize(mensaje, OpcionesJson),
            _reloj.UtcNow);

        await _db.OutboxMessages.AddAsync(fila, cancellationToken);
    }
}
