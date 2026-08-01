namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de escritura del patron Outbox. El mensaje se adjunta a la MISMA
/// unidad de trabajo que la entidad, de modo que "guardar la admision" y
/// "prometer publicar el evento" son atomicos: o pasan los dos, o ninguno.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Encola un mensaje. No persiste por si mismo; requiere el commit del
    /// <see cref="IUnitOfWork"/>.
    /// </summary>
    Task EncolarAsync<TMensaje>(TMensaje mensaje, CancellationToken cancellationToken)
        where TMensaje : class;
}
