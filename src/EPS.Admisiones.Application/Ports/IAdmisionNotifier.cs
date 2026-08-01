using EPS.Admisiones.Application.Contracts;

namespace EPS.Admisiones.Application.Ports;

/// <summary>
/// Puerto de notificacion en tiempo real hacia los dashboards conectados.
/// Se dispara SOLO cuando la admision quedo consistente en ambos almacenes,
/// para que auditoria nunca vea una fila que aun podria fallar.
/// </summary>
public interface IAdmisionNotifier
{
    Task NotificarAsync(AdmisionResumen admision, CancellationToken cancellationToken);
}
