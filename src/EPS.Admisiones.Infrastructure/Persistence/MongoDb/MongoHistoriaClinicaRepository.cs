using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EPS.Admisiones.Infrastructure.Persistence.MongoDb;

/// <summary>
/// Adaptador de <see cref="IHistoriaClinicaRepository"/> sobre MongoDB
/// (equivalente a Azure Cosmos DB for MongoDB).
///
/// Se trabaja con <see cref="BsonDocument"/> y no con una clase POCO porque el
/// esquema lo define el estandar FHIR: mapearlo a tipos fijos obligaria a
/// desplegar cada vez que un prestador agregue un campo.
/// </summary>
public sealed class MongoHistoriaClinicaRepository : IHistoriaClinicaRepository
{
    private const string CampoContenido = "contenido";
    private const string CampoContenidoOriginal = "contenidoOriginal";

    private readonly IMongoCollection<BsonDocument> _coleccion;

    public MongoHistoriaClinicaRepository(IMongoClient cliente, IOptions<MongoOptions> opciones)
    {
        var configuracion = opciones.Value;

        _coleccion = cliente
            .GetDatabase(configuracion.BaseDatos)
            .GetCollection<BsonDocument>(configuracion.ColeccionHistoriasClinicas);
    }

    public async Task GuardarAsync(HistoriaClinica historiaClinica, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(historiaClinica);

        var documento = new BsonDocument
        {
            { "_id", historiaClinica.Id },
            { "tipoDocumento", historiaClinica.Documento.Tipo.ToString() },
            { "numeroDocumento", historiaClinica.Documento.Numero },
            { "recursoFhir", historiaClinica.RecursoFhir },
            { "capturadaEnUtc", historiaClinica.CapturadaEnUtc },
            // Version consultable y indexable del payload.
            { CampoContenido, BsonDocument.Parse(historiaClinica.ContenidoJson) },
            // Copia literal del JSON recibido. BsonDocument.Parse convierte los
            // numeros JSON a double, asi que conservar el texto original es lo
            // que garantiza fidelidad clinica y trazabilidad ante auditoria.
            { CampoContenidoOriginal, historiaClinica.ContenidoJson }
        };

        // Upsert por _id: el despachador del Outbox entrega AL MENOS UNA VEZ,
        // asi que esta operacion debe poder repetirse sin duplicar ni corromper.
        await _coleccion.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", historiaClinica.Id),
            documento,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<string?> ObtenerContenidoAsync(
        string historiaClinicaId,
        CancellationToken cancellationToken)
    {
        var documento = await _coleccion
            .Find(Builders<BsonDocument>.Filter.Eq("_id", historiaClinicaId))
            .Project(Builders<BsonDocument>.Projection.Include(CampoContenidoOriginal))
            .FirstOrDefaultAsync(cancellationToken);

        return documento is null || !documento.TryGetValue(CampoContenidoOriginal, out var valor)
            ? null
            : valor.AsString;
    }
}
