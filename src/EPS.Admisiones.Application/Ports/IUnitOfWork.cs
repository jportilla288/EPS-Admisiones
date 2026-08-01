namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Frontera transaccional. Todo lo que se haya adjuntado a los repositorios y
/// al Outbox se confirma en UNA sola transaccion de SQL Server: es la pieza
/// que elimina la ventana de inconsistencia del dual write.
/// </summary>
public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken);
}
