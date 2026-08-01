using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Common;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Pacientes;

/// <summary>
/// Afiliado a la EPS. Raiz del agregado que agrupa sus atenciones facturables;
/// es el modelo sobre el que corre el reporte mensual de auditoria (Parte 4).
/// </summary>
public sealed class Paciente : AggregateRoot
{
    private readonly List<Atencion> _atenciones = [];

    private Paciente()
    {
    }

    private Paciente(Guid id, DocumentoPaciente documento, string nombre, string apellido)
    {
        Id = id;
        Documento = documento;
        Nombre = nombre;
        Apellido = apellido;
        Estado = EstadoPaciente.Activo;
    }

    public Guid Id { get; private set; }

    public DocumentoPaciente Documento { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    public string Apellido { get; private set; } = null!;

    public EstadoPaciente Estado { get; private set; }

    /// <summary>
    /// Solo lectura hacia afuera: las atenciones se agregan por
    /// <see cref="RegistrarAtencion"/>, que es donde viven las invariantes.
    /// </summary>
    public IReadOnlyCollection<Atencion> Atenciones => _atenciones.AsReadOnly();

    public static Paciente Registrar(DocumentoPaciente documento, string? nombre, string? apellido)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del paciente es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(apellido))
        {
            throw new DomainException("El apellido del paciente es obligatorio.");
        }

        return new Paciente(Guid.NewGuid(), documento, nombre.Trim(), apellido.Trim());
    }

    public Atencion RegistrarAtencion(
        Guid admisionId,
        ValorCopago valor,
        bool requiereAuditoria,
        DateTime fechaUtc)
    {
        ArgumentNullException.ThrowIfNull(valor);

        if (Estado != EstadoPaciente.Activo)
        {
            throw new DomainException(
                $"No se pueden registrar atenciones a un paciente en estado {Estado}.");
        }

        var existente = _atenciones.SingleOrDefault(a => a.AdmisionId == admisionId);

        if (existente is not null)
        {
            // Idempotencia dentro del agregado: reintentar la misma admision
            // no duplica la atencion facturable.
            return existente;
        }

        var atencion = new Atencion(
            Guid.NewGuid(),
            Id,
            admisionId,
            valor,
            requiereAuditoria,
            fechaUtc);

        _atenciones.Add(atencion);

        return atencion;
    }

    public void ActualizarNombre(string? nombre, string? apellido)
    {
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            Nombre = nombre.Trim();
        }

        if (!string.IsNullOrWhiteSpace(apellido))
        {
            Apellido = apellido.Trim();
        }
    }
}
