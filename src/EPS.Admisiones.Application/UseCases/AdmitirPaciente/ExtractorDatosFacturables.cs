using System.Globalization;
using System.Text.Json;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Application.UseCases.AdmitirPaciente;

/// <summary>
/// Implementacion sobre el perfil simplificado documentado en el README:
/// <code>
/// {
///   "resourceType": "Bundle",
///   "paciente":  { "tipoDocumento": "CC", "numeroDocumento": "1098765432",
///                  "nombre": "Ana", "apellido": "Portilla" },
///   "copago":    { "valor": 25000.00, "moneda": "COP" },
///   "encuentro": { "requiereAuditoria": true, ... }
/// }
/// </code>
/// Es logica pura sobre JSON: sin dependencias de infraestructura, por lo que
/// vive en Application y se puede testear de forma aislada.
/// </summary>
public sealed class ExtractorDatosFacturables : IExtractorDatosFacturables
{
    public DatosFacturables Extraer(JsonElement historiaClinica)
    {
        if (historiaClinica.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException("La historia clinica debe ser un objeto JSON.");
        }

        var paciente = ObtenerObjetoRequerido(historiaClinica, "paciente");
        var copagoNodo = ObtenerObjetoRequerido(historiaClinica, "copago");

        var documento = DocumentoPaciente.Crear(
            ObtenerTextoRequerido(paciente, "tipoDocumento", "paciente.tipoDocumento"),
            ObtenerTextoRequerido(paciente, "numeroDocumento", "paciente.numeroDocumento"));

        var copago = ValorCopago.Crear(
            ObtenerDecimalRequerido(copagoNodo, "valor", "copago.valor"),
            ObtenerTextoOpcional(copagoNodo, "moneda") ?? ValorCopago.MonedaPorDefecto);

        var requiereAuditoria =
            historiaClinica.TryGetProperty("encuentro", out var encuentro)
            && encuentro.ValueKind == JsonValueKind.Object
            && encuentro.TryGetProperty("requiereAuditoria", out var bandera)
            && bandera.ValueKind == JsonValueKind.True;

        var recurso = ObtenerTextoOpcional(historiaClinica, "resourceType") ?? "Bundle";

        return new DatosFacturables(
            documento,
            copago,
            ObtenerTextoRequerido(paciente, "nombre", "paciente.nombre"),
            ObtenerTextoRequerido(paciente, "apellido", "paciente.apellido"),
            requiereAuditoria,
            recurso);
    }

    private static JsonElement ObtenerObjetoRequerido(JsonElement raiz, string propiedad)
    {
        if (!raiz.TryGetProperty(propiedad, out var nodo) || nodo.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException(
                $"La historia clinica no contiene el objeto obligatorio '{propiedad}'.");
        }

        return nodo;
    }

    private static string ObtenerTextoRequerido(JsonElement nodo, string propiedad, string ruta)
    {
        if (!nodo.TryGetProperty(propiedad, out var valor) || valor.ValueKind != JsonValueKind.String)
        {
            throw new DomainException($"Falta el campo obligatorio '{ruta}' o no es texto.");
        }

        return valor.GetString()!;
    }

    private static string? ObtenerTextoOpcional(JsonElement nodo, string propiedad) =>
        nodo.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static decimal ObtenerDecimalRequerido(JsonElement nodo, string propiedad, string ruta)
    {
        if (!nodo.TryGetProperty(propiedad, out var valor))
        {
            throw new DomainException($"Falta el campo obligatorio '{ruta}'.");
        }

        // Se acepta numero o cadena: distintos motores FHIR serializan el monto de ambas formas.
        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetDecimal(out var numero) => numero,
            JsonValueKind.String when decimal.TryParse(
                valor.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var texto) => texto,
            _ => throw new DomainException($"El campo '{ruta}' no es un valor monetario valido.")
        };
    }
}
