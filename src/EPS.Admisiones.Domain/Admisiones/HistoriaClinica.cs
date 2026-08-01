using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Admisiones;

/// <summary>
/// Historia clinica en formato FHIR/HL7. Es deliberadamente un contenedor
/// semi-estructurado: el esquema lo define el estandar, no nosotros, y por eso
/// vive en un almacen documental. El dominio solo garantiza que exista un
/// identificador estable, un paciente y un contenido no vacio.
/// </summary>
public sealed class HistoriaClinica
{
    private HistoriaClinica(
        string id,
        DocumentoPaciente documento,
        string recursoFhir,
        string contenidoJson,
        DateTime capturadaEnUtc)
    {
        Id = id;
        Documento = documento;
        RecursoFhir = recursoFhir;
        ContenidoJson = contenidoJson;
        CapturadaEnUtc = capturadaEnUtc;
    }

    /// <summary>Mismo valor que <c>Admision.HistoriaClinicaId</c>.</summary>
    public string Id { get; }

    public DocumentoPaciente Documento { get; }

    /// <summary>Tipo de recurso FHIR raiz, p. ej. "Bundle" o "Encounter".</summary>
    public string RecursoFhir { get; }

    /// <summary>Payload original sin recortar. Se conserva integro por trazabilidad clinica.</summary>
    public string ContenidoJson { get; }

    public DateTime CapturadaEnUtc { get; }

    public static HistoriaClinica Crear(
        string id,
        DocumentoPaciente documento,
        string? recursoFhir,
        string? contenidoJson,
        DateTime capturadaEnUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainException("La historia clinica requiere un identificador.");
        }

        ArgumentNullException.ThrowIfNull(documento);

        if (string.IsNullOrWhiteSpace(contenidoJson))
        {
            throw new DomainException("La historia clinica no puede tener contenido vacio.");
        }

        var recurso = string.IsNullOrWhiteSpace(recursoFhir) ? "Bundle" : recursoFhir.Trim();

        return new HistoriaClinica(id, documento, recurso, contenidoJson, capturadaEnUtc);
    }
}
