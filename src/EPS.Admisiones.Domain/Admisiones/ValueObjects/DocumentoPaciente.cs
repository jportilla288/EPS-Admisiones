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

        if (!normalizado.All(char.IsLetterOrDigit))
        {
            throw new DomainException("El numero de documento solo admite letras y digitos.");
        }

        return new DocumentoPaciente(tipoDocumento, normalizado);
    }

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
