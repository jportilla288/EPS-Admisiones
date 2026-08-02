using EPS.Admisiones.Domain.Common;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Admisiones.ValueObjects;

/// <summary>
/// Documento de identidad del paciente. Se modela como Value Object para que
/// la validacion viva en un solo lugar y sea imposible construir uno invalido.
/// </summary>
public sealed class DocumentoPaciente : ValueObject
{
    public const int LongitudMaxima = 20;

    /// <summary>
    /// Ningun documento de identidad colombiano vigente tiene menos de cinco
    /// caracteres. Sin este piso, valores de relleno como "0" o "123" entran al
    /// sistema y contaminan la identidad del afiliado, que es la clave por la
    /// que se buscan sus admisiones y su historia clinica.
    /// </summary>
    public const int LongitudMinima = 5;

    private DocumentoPaciente(TipoDocumento tipo, string numero)
    {
        Tipo = tipo;
        Numero = numero;
    }

    public TipoDocumento Tipo { get; }

    public string Numero { get; }

    public static DocumentoPaciente Crear(string tipo, string? numero)
    {
        if (!Enum.TryParse<TipoDocumento>(tipo, ignoreCase: true, out var tipoDocumento))
        {
            throw new DomainException($"Tipo de documento no reconocido: '{tipo}'.");
        }

        if (string.IsNullOrWhiteSpace(numero))
        {
            throw new DomainException("El numero de documento del paciente es obligatorio.");
        }

        var normalizado = numero.Trim().ToUpperInvariant();

        if (normalizado.Length > LongitudMaxima)
        {
            throw new DomainException(
                $"El numero de documento no puede exceder {LongitudMaxima} caracteres.");
        }

        if (normalizado.Length < LongitudMinima)
        {
            throw new DomainException(
                $"El numero de documento debe tener al menos {LongitudMinima} caracteres.");
        }

        if (!normalizado.All(char.IsLetterOrDigit))
        {
            throw new DomainException("El numero de documento solo admite letras y digitos.");
        }

        // "0000" supera todas las validaciones anteriores y aun asi no
        // identifica a nadie: es el valor de relleno tipico cuando quien digita
        // no tiene el documento a mano.
        if (normalizado.All(caracter => caracter == '0'))
        {
            throw new DomainException("El numero de documento no puede ser una secuencia de ceros.");
        }

        // Cedula de ciudadania, tarjeta de identidad y registro civil son
        // numericos por definicion. Cedula de extranjeria y pasaporte admiten
        // letras, asi que la regla se aplica solo donde corresponde.
        if (SoloAdmiteDigitos(tipoDocumento) && !normalizado.All(char.IsDigit))
        {
            throw new DomainException(
                $"El numero de documento para {tipoDocumento} solo admite digitos.");
        }

        return new DocumentoPaciente(tipoDocumento, normalizado);
    }

    private static bool SoloAdmiteDigitos(TipoDocumento tipo) =>
        tipo is TipoDocumento.CC or TipoDocumento.TI or TipoDocumento.RC;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Tipo;
        yield return Numero;
    }

    public override string ToString() => $"{Tipo}-{Numero}";
}

/// <summary>
/// Tipos de documento habilitados en Colombia para afiliados a una EPS.
/// </summary>
public enum TipoDocumento
{
    /// <summary>Cedula de ciudadania.</summary>
    CC = 1,

    /// <summary>Cedula de extranjeria.</summary>
    CE = 2,

    /// <summary>Tarjeta de identidad.</summary>
    TI = 3,

    /// <summary>Registro civil.</summary>
    RC = 4,

    /// <summary>Pasaporte.</summary>
    PA = 5
}
