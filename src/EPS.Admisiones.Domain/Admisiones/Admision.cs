using EPS.Admisiones.Domain.Admisiones.Events;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Common;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Domain.Admisiones;

/// <summary>
/// Raiz de agregado del modulo. Representa el registro transaccional que
/// bloquea el copago del afiliado y la referencia a su historia clinica.
/// </summary>
public sealed class Admision : AggregateRoot
{
    /// <summary>Constructor para EF Core. No usar desde codigo de aplicacion.</summary>
    private Admision()
    {
    }

    private Admision(
        Guid id,
        Guid pacienteId,
        DocumentoPaciente documento,
        ValorCopago copago,
        string historiaClinicaId,
        DateTime fechaAdmisionUtc)
    {
        Id = id;
        PacienteId = pacienteId;
        Documento = documento;
        Copago = copago;
        HistoriaClinicaId = historiaClinicaId;
        FechaAdmisionUtc = fechaAdmisionUtc;
        Estado = EstadoAdmision.PendienteSincronizacion;
        IntentosSincronizacion = 0;
    }

    public Guid Id { get; private set; }

    /// <summary>Afiliado al que pertenece la admision (FK hacia Pacientes).</summary>
    public Guid PacienteId { get; private set; }

    /// <summary>
    /// Documento desnormalizado a proposito: el reporte operativo de admisiones
    /// lo consulta miles de veces al dia y evitar el JOIN a Pacientes es una
    /// optimizacion consciente, no un descuido de normalizacion.
    /// </summary>
    public DocumentoPaciente Documento { get; private set; } = null!;

    public ValorCopago Copago { get; private set; } = null!;

    /// <summary>
    /// Identificador del documento en MongoDB. Se calcula en el momento de la
    /// admision (no lo asigna Mongo) para que la escritura sea idempotente:
    /// el Outbox puede reintentar N veces y siempre hace upsert sobre el mismo _id.
    /// </summary>
    public string HistoriaClinicaId { get; private set; } = null!;

    public DateTime FechaAdmisionUtc { get; private set; }

    public EstadoAdmision Estado { get; private set; }

    public int IntentosSincronizacion { get; private set; }

    public DateTime? SincronizadaEnUtc { get; private set; }

    public string? MotivoFallo { get; private set; }

    /// <summary>Token de concurrencia optimista (rowversion en SQL Server).</summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>
    /// Unica forma de crear una admision. Deja el agregado en estado
    /// PendienteSincronizacion y emite <see cref="PacienteAdmitido"/>.
    /// </summary>
    public static Admision Registrar(
        Guid pacienteId,
        DocumentoPaciente documento,
        ValorCopago copago,
        DateTime fechaAdmisionUtc,
        Guid? admisionId = null)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(copago);

        if (pacienteId == Guid.Empty)
        {
            throw new DomainException("La admision debe asociarse a un paciente.");
        }

        if (fechaAdmisionUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("La fecha de admision debe expresarse en UTC.");
        }

        var id = admisionId ?? Guid.NewGuid();

        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de la admision no puede ser vacio.");
        }

        var admision = new Admision(
            id,
            pacienteId,
            documento,
            copago,
            id.ToString("N"),
            fechaAdmisionUtc);

        admision.RegistrarEvento(new PacienteAdmitido(
            admision.Id,
            admision.HistoriaClinicaId,
            admision.Documento,
            admision.Copago,
            admision.FechaAdmisionUtc));

        return admision;
    }

    /// <summary>
    /// La historia clinica quedo confirmada en el almacen documental.
    /// Idempotente: repetir la confirmacion no altera el agregado.
    /// </summary>
    public void ConfirmarSincronizacion(DateTime confirmadaEnUtc)
    {
        if (Estado == EstadoAdmision.Sincronizada)
        {
            return;
        }

        Estado = EstadoAdmision.Sincronizada;
        SincronizadaEnUtc = confirmadaEnUtc;
        MotivoFallo = null;
    }

    /// <summary>
    /// Registra un intento fallido de sincronizacion. Tras agotar los reintentos
    /// la admision queda marcada para conciliacion manual, pero NUNCA se pierde:
    /// el copago ya esta bloqueado en SQL Server.
    /// </summary>
    public void RegistrarFalloSincronizacion(string motivo, int maximoIntentos)
    {
        if (maximoIntentos <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximoIntentos));
        }

        IntentosSincronizacion++;
        MotivoFallo = motivo;

        if (IntentosSincronizacion >= maximoIntentos)
        {
            Estado = EstadoAdmision.FallidaSincronizacion;
        }
    }
}
