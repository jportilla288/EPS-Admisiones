using EPS.Admisiones.Domain.Common;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;

namespace EPS.Admisiones.Domain.Admisiones.Events;

/// <summary>
/// Se emite cuando la admision quedo registrada en el almacen transaccional
/// con el copago bloqueado. No transporta el payload FHIR: solo identidad y
/// datos ya validados. El payload viaja por el Outbox, que es infraestructura.
/// </summary>
public sealed record PacienteAdmitido(
    Guid AdmisionId,
    string HistoriaClinicaId,
    DocumentoPaciente Documento,
    ValorCopago Copago,
    DateTime FechaAdmisionUtc) : IDomainEvent
{
    public Guid EventoId { get; } = Guid.NewGuid();

    public DateTime OcurridoEnUtc { get; } = DateTime.UtcNow;
}
