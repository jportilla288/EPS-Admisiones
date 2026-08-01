using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Pacientes;

namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de salida del agregado Paciente. Comparte la unidad de trabajo con
/// <see cref="IAdmisionRepository"/>, de modo que paciente, atencion, admision
/// y mensaje de Outbox se confirman en una unica transaccion.
/// </summary>
public interface IPacienteRepository
{
    /// <summary>Carga el paciente con sus atenciones para poder aplicar invariantes.</summary>
    Task<Paciente?> ObtenerPorDocumentoAsync(
        DocumentoPaciente documento,
        CancellationToken cancellationToken);

    Task AgregarAsync(Paciente paciente, CancellationToken cancellationToken);
}
