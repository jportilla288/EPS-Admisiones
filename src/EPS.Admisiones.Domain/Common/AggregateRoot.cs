namespace EPS.Admisiones.Domain.Common;

/// <summary>
/// Raiz de agregado: unidad de consistencia transaccional y unico punto de
/// entrada para modificar el estado interno.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _eventos = [];

    public IReadOnlyCollection<IDomainEvent> EventosDeDominio => _eventos.AsReadOnly();

    protected void RegistrarEvento(IDomainEvent evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();
}
