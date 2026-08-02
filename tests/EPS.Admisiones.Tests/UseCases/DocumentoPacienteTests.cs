using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Tests.UseCases;

/// <summary>
/// El documento es la clave por la que se identifican las admisiones y la
/// historia clinica de un afiliado. Estos tests protegen esa identidad de los
/// valores de relleno que un formulario acepta con demasiada facilidad.
/// </summary>
public sealed class DocumentoPacienteTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0000")]
    [InlineData("00000")]
    [InlineData("0000000000")]
    public void Rechaza_las_secuencias_de_ceros(string numero)
    {
        Assert.Throws<DomainException>(() => DocumentoPaciente.Crear("CC", numero));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("1234")]
    public void Rechaza_los_documentos_demasiado_cortos(string numero)
    {
        var error = Assert.Throws<DomainException>(() => DocumentoPaciente.Crear("CC", numero));

        Assert.Contains("al menos", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CC")]
    [InlineData("TI")]
    [InlineData("RC")]
    public void Los_documentos_colombianos_numericos_no_admiten_letras(string tipo)
    {
        var error = Assert.Throws<DomainException>(
            () => DocumentoPaciente.Crear(tipo, "AB1234567"));

        Assert.Contains("digitos", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CE", "AB1234567")]
    [InlineData("PA", "XY987654")]
    public void El_pasaporte_y_la_cedula_de_extranjeria_si_admiten_letras(
        string tipo,
        string numero)
    {
        var documento = DocumentoPaciente.Crear(tipo, numero);

        Assert.Equal(numero, documento.Numero);
    }

    [Fact]
    public void Normaliza_espacios_y_mayusculas()
    {
        var documento = DocumentoPaciente.Crear("pa", "  ab987654  ");

        Assert.Equal("AB987654", documento.Numero);
        Assert.Equal(TipoDocumento.PA, documento.Tipo);
    }

    [Fact]
    public void Acepta_una_cedula_valida()
    {
        var documento = DocumentoPaciente.Crear("CC", "1098765432");

        Assert.Equal(TipoDocumento.CC, documento.Tipo);
        Assert.Equal("1098765432", documento.Numero);
    }

    /// <summary>
    /// Un cero como parte del numero es perfectamente valido; lo que se rechaza
    /// es que TODO el documento sean ceros.
    /// </summary>
    [Fact]
    public void Un_documento_que_empieza_por_cero_sigue_siendo_valido()
    {
        var documento = DocumentoPaciente.Crear("CC", "0012345");

        Assert.Equal("0012345", documento.Numero);
    }
}
