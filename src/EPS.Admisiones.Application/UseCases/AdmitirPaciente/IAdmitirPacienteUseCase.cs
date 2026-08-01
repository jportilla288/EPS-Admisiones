namespace EPS.Admisiones.Application.UseCases.AdmitirPaciente;

/// <summary>
/// Puerto de ENTRADA del modulo. Tanto el controlador REST como el dashboard
/// de Blazor dependen de esta interfaz, no de la implementacion.
/// </summary>
public interface IAdmitirPacienteUseCase
{
    Task<AdmitirPacienteResult> EjecutarAsync(
        AdmitirPacienteCommand command,
        CancellationToken cancellationToken);
}
