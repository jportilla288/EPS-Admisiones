using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Pacientes;

/// <summary>
/// Atencion facturable derivada de una admision. Pertenece al agregado
/// <see cref="Paciente"/>: no se crea ni se modifica por fuera de el.
/// </summary>
public sealed class Atencion
{
    private Atencion()
    {
    }

    internal Atencion(
        Guid id,
        Guid pacienteId,
        Guid admisionId,
        ValorCopago valor,
        bool requiereAuditoria,
        DateTime fechaUtc)
    {
        Id = id;
        PacienteId = pacienteId;
        AdmisionId = admisionId;
        Valor = valor;
        RequiereAuditoria = requiereAuditoria;
        FechaUtc = fechaUtc;
    }

    public Guid Id { get; private set; }

    public Guid PacienteId { get; private set; }

    public Guid AdmisionId { get; private set; }

    public ValorCopago Valor { get; private set; } = null!;

    public bool RequiereAuditoria { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public void MarcarParaAuditoria()
    {
        if (Valor.Monto <= 0m)
        {
            throw new DomainException("No se audita una atencion sin valor asociado.");
        }

        RequiereAuditoria = true;
    }
}
