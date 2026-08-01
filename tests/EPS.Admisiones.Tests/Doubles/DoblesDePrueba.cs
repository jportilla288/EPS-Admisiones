using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Pacientes;

namespace EPS.Admisiones.Tests.Doubles;

/// <summary>
/// Bitacora compartida por los dobles. Permite afirmar sobre el ORDEN de las
/// operaciones, que es justo lo que hay que verificar en el patron Outbox:
/// el commit tiene que ocurrir DESPUES de encolar el mensaje, no antes.
/// </summary>
public sealed class BitacoraOperaciones
{
    private readonly List<string> _operaciones = [];

    public IReadOnlyList<string> Operaciones => _operaciones;

    public void Registrar(string operacion) => _operaciones.Add(operacion);
}

public sealed class AdmisionRepositoryFalso : IAdmisionRepository
{
    private readonly BitacoraOperaciones _bitacora;

    public AdmisionRepositoryFalso(BitacoraOperaciones bitacora) => _bitacora = bitacora;

    public List<Admision> Agregadas { get; } = [];

    public Dictionary<Guid, Admision> Existentes { get; } = [];

    public Task<Admision?> ObtenerAsync(Guid admisionId, CancellationToken cancellationToken)
    {
        _bitacora.Registrar($"ObtenerAdmision:{admisionId}");

        return Task.FromResult(Existentes.GetValueOrDefault(admisionId));
    }

    public Task AgregarAsync(Admision admision, CancellationToken cancellationToken)
    {
        _bitacora.Registrar("AgregarAdmision");
        Agregadas.Add(admision);

        return Task.CompletedTask;
    }
}

public sealed class PacienteRepositoryFalso : IPacienteRepository
{
    private readonly BitacoraOperaciones _bitacora;

    public PacienteRepositoryFalso(BitacoraOperaciones bitacora) => _bitacora = bitacora;

    public List<Paciente> Agregados { get; } = [];

    public Paciente? Existente { get; set; }

    public Task<Paciente?> ObtenerPorDocumentoAsync(
        DocumentoPaciente documento,
        CancellationToken cancellationToken)
    {
        _bitacora.Registrar("ObtenerPaciente");

        return Task.FromResult(Existente);
    }

    public Task AgregarAsync(Paciente paciente, CancellationToken cancellationToken)
    {
        _bitacora.Registrar("AgregarPaciente");
        Agregados.Add(paciente);

        return Task.CompletedTask;
    }
}

public sealed class OutboxWriterFalso : IOutboxWriter
{
    private readonly BitacoraOperaciones _bitacora;

    public OutboxWriterFalso(BitacoraOperaciones bitacora) => _bitacora = bitacora;

    public List<object> Encolados { get; } = [];

    public Task EncolarAsync<TMensaje>(TMensaje mensaje, CancellationToken cancellationToken)
        where TMensaje : class
    {
        _bitacora.Registrar("EncolarOutbox");
        Encolados.Add(mensaje);

        return Task.CompletedTask;
    }
}

public sealed class UnitOfWorkFalso : IUnitOfWork
{
    private readonly BitacoraOperaciones _bitacora;

    public UnitOfWorkFalso(BitacoraOperaciones bitacora) => _bitacora = bitacora;

    public int Confirmaciones { get; private set; }

    /// <summary>Permite simular una caida de SQL Server en el commit.</summary>
    public Exception? ExcepcionAlGuardar { get; set; }

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        _bitacora.Registrar("Commit");

        if (ExcepcionAlGuardar is not null)
        {
            return Task.FromException<int>(ExcepcionAlGuardar);
        }

        Confirmaciones++;

        return Task.FromResult(1);
    }
}

public sealed class RelojFijo : IRelojSistema
{
    public RelojFijo(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
