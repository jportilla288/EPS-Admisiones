namespace EPS.Admisiones.Domain.Pacientes;

/// <summary>
/// Estado de afiliacion. Se modela como enum y se persiste como int:
/// el codigo original de la Parte 4 comparaba contra la cadena "Activo",
/// lo que impide usar indices eficientes y rompe en silencio ante un typo.
/// </summary>
public enum EstadoPaciente
{
    Activo = 1,
    Inactivo = 2,
    Retirado = 3
}
