using EPS.Admisiones.Application.UseCases.AdmitirPaciente;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;
using EPS.Admisiones.Tests.Doubles;

namespace EPS.Admisiones.Tests.UseCases;

public sealed class ExtractorDatosFacturablesTests
{
    private readonly ExtractorDatosFacturables _extractor = new();

    [Fact]
    public void Extrae_documento_copago_y_bandera_de_auditoria()
    {
        var datos = _extractor.Extraer(
            PayloadsDePrueba.HistoriaClinicaValida(copago: 18500.50m, requiereAuditoria: true));

        Assert.Equal(TipoDocumento.CC, datos.Documento.Tipo);
        Assert.Equal(18500.50m, datos.Copago.Monto);
        Assert.True(datos.RequiereAuditoria);
        Assert.Equal("Ana Maria", datos.Nombre);
    }

    [Theory]
    [InlineData("\"25000.00\"")] // algunos motores FHIR serializan el monto como texto
    [InlineData("25000.00")]
    public void Acepta_el_monto_como_numero_o_como_cadena(string monto)
    {
        var payload = PayloadsDePrueba.Json($$"""
        {
          "paciente": { "tipoDocumento": "CC", "numeroDocumento": "123", "nombre": "A", "apellido": "B" },
          "copago": { "valor": {{monto}} }
        }
        """);

        var datos = _extractor.Extraer(payload);

        Assert.Equal(25000.00m, datos.Copago.Monto);
        Assert.Equal(ValorCopago.MonedaPorDefecto, datos.Copago.Moneda);
    }

    [Fact]
    public void Rechaza_un_payload_sin_el_nodo_paciente()
    {
        var payload = PayloadsDePrueba.Json("""{ "copago": { "valor": 1000 } }""");

        var error = Assert.Throws<DomainException>(() => _extractor.Extraer(payload));

        Assert.Contains("paciente", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rechaza_un_copago_negativo()
    {
        var payload = PayloadsDePrueba.Json("""
        {
          "paciente": { "tipoDocumento": "CC", "numeroDocumento": "123", "nombre": "A", "apellido": "B" },
          "copago": { "valor": -5000 }
        }
        """);

        Assert.Throws<DomainException>(() => _extractor.Extraer(payload));
    }

    [Fact]
    public void Rechaza_un_tipo_de_documento_desconocido()
    {
        var payload = PayloadsDePrueba.Json("""
        {
          "paciente": { "tipoDocumento": "XX", "numeroDocumento": "123", "nombre": "A", "apellido": "B" },
          "copago": { "valor": 1000 }
        }
        """);

        Assert.Throws<DomainException>(() => _extractor.Extraer(payload));
    }
}
