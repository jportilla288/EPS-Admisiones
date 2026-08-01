using System.Text.Json;

namespace EPS.Admisiones.Application.UseCases.AdmitirPaciente;

/// <summary>
/// Entrada del caso de uso. Transporta el payload FHIR/HL7 tal cual llego,
/// sin recortarlo: de ahi se extraen el documento del paciente y el copago.
/// </summary>
/// <param name="HistoriaClinica">Documento clinico anidado, en crudo.</param>
/// <param name="AdmisionId">
/// Clave de idempotencia suministrada por el cliente (cabecera Idempotency-Key).
/// Si se repite, la operacion devuelve la admision existente en lugar de duplicarla:
/// imprescindible cuando el cliente reintenta por un timeout de red.
/// </param>
public sealed record AdmitirPacienteCommand(JsonElement HistoriaClinica, Guid? AdmisionId = null);

/// <summary>Salida del caso de uso.</summary>
public sealed record AdmitirPacienteResult(
    Guid AdmisionId,
    string HistoriaClinicaId,
    string Estado,
    DateTime FechaAdmisionUtc,
    bool EraDuplicado);
