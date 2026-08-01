using System.ComponentModel.DataAnnotations;

namespace EPS.Admisiones.Infrastructure.Persistence.MongoDb;

/// <summary>
/// Configuracion del almacen documental. En Azure apunta a Cosmos DB for MongoDB
/// y la cadena NUNCA sale de Key Vault (ver docs/arquitectura-azure.md).
/// </summary>
public sealed class MongoOptions
{
    public const string SeccionConfiguracion = "Mongo";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string BaseDatos { get; set; } = "eps_admisiones";

    [Required]
    public string ColeccionHistoriasClinicas { get; set; } = "historias_clinicas";
}
