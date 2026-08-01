using System.Text.Json;

namespace EPS.Admisiones.Tests.Doubles;

/// <summary>Constructor de payloads FHIR simulados para los tests.</summary>
public static class PayloadsDePrueba
{
    public static JsonElement HistoriaClinicaValida(
        string tipoDocumento = "CC",
        string numeroDocumento = "1098765432",
        decimal copago = 25000.00m,
        bool requiereAuditoria = true)
    {
        var json = $$"""
        {
          "resourceType": "Bundle",
          "paciente": {
            "tipoDocumento": "{{tipoDocumento}}",
            "numeroDocumento": "{{numeroDocumento}}",
            "nombre": "Ana Maria",
            "apellido": "Portilla"
          },
          "copago": {
            "valor": {{copago.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
            "moneda": "COP"
          },
          "encuentro": {
            "clase": "ambulatorio",
            "requiereAuditoria": {{(requiereAuditoria ? "true" : "false")}},
            "diagnosticos": [
              { "codigo": "J06.9", "descripcion": "Infeccion aguda de vias respiratorias" }
            ]
          }
        }
        """;

        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public static JsonElement Json(string contenido) =>
        JsonDocument.Parse(contenido).RootElement.Clone();
}
