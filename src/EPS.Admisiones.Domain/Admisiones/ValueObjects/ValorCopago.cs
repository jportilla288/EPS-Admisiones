using EPS.Admisiones.Domain.Common;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Admisiones.ValueObjects;

/// <summary>
/// Valor monetario del copago a bloquear en la cuenta del afiliado.
/// Se guarda como decimal (nunca double) por tratarse de dinero.
/// </summary>
public sealed class ValorCopago : ValueObject
{
    public const string MonedaPorDefecto = "COP";

    private ValorCopago(decimal monto, string moneda)
    {
        Monto = monto;
        Moneda = moneda;
    }

    public decimal Monto { get; }

    public string Moneda { get; }

    public static ValorCopago Crear(decimal monto, string? moneda = MonedaPorDefecto)
    {
        if (monto < 0m)
        {
            throw new DomainException("El valor del copago no puede ser negativo.");
        }

        if (decimal.Round(monto, 2) != monto)
        {
            throw new DomainException("El valor del copago admite maximo 2 decimales.");
        }

        var codigo = string.IsNullOrWhiteSpace(moneda)
            ? MonedaPorDefecto
            : moneda.Trim().ToUpperInvariant();

        if (codigo.Length != 3)
        {
            throw new DomainException("La moneda debe ser un codigo ISO 4217 de 3 letras.");
        }

        return new ValorCopago(monto, codigo);
    }

    public static ValorCopago Cero() => new(0m, MonedaPorDefecto);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Monto;
        yield return Moneda;
    }

    public override string ToString() => $"{Monto:0.00} {Moneda}";
}
