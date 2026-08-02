using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.UseCases.ConsultarDetalleAdmision;
using EPS.Admisiones.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EPS.Admisiones.Tests.UseCases;

/// <summary>
/// Tests de la lectura poliglota: SQL Server manda sobre la existencia de la
/// admision, MongoDB solo enriquece la respuesta.
/// </summary>
public sealed class ConsultarDetalleAdmisionUseCaseTests
{
    private static readonly Guid AdmisionId = new("3f1c9a52-7d4b-4d2e-9a11-6c0f2b8e4d10");
    private static readonly DateTime Ahora = new(2026, 7, 31, 14, 30, 0, DateTimeKind.Utc);

    private readonly AdmisionesQueryFalso _consultas = new();
    private readonly HistoriaClinicaRepositoryFalso _historias = new();
    private readonly ConsultarDetalleAdmisionUseCase _useCase;

    public ConsultarDetalleAdmisionUseCaseTests()
    {
        _useCase = new ConsultarDetalleAdmisionUseCase(
            _consultas,
            _historias,
            NullLogger<ConsultarDetalleAdmisionUseCase>.Instance);
    }

    [Fact]
    public async Task Combina_el_registro_de_SQL_con_la_historia_clinica_de_Mongo()
    {
        _consultas.Registro = RegistroDePrueba();
        _historias.Contenido = """{"resourceType":"Bundle"}""";

        var detalle = await _useCase.EjecutarAsync(AdmisionId, CancellationToken.None);

        Assert.NotNull(detalle);
        Assert.Equal("Ana Maria Portilla", detalle.NombreCompleto);
        Assert.Equal(25000m, detalle.ValorCopago);
        Assert.Equal("""{"resourceType":"Bundle"}""", detalle.HistoriaClinicaJson);
        Assert.True(detalle.TieneHistoriaClinica);
    }

    [Fact]
    public async Task Una_admision_inexistente_devuelve_null_y_no_toca_el_almacen_documental()
    {
        _consultas.Registro = null;
        _historias.ExcepcionAlLeer = new InvalidOperationException(
            "No debio consultarse el almacen documental.");

        var detalle = await _useCase.EjecutarAsync(AdmisionId, CancellationToken.None);

        Assert.Null(detalle);
    }

    /// <summary>
    /// La garantia que sostiene la decision de disenar el detalle asi: el dato
    /// financiero vive en SQL Server y no puede quedar inaccesible porque el
    /// almacen documental este caido.
    /// </summary>
    [Fact]
    public async Task Si_Mongo_falla_devuelve_el_registro_transaccional_sin_la_historia()
    {
        _consultas.Registro = RegistroDePrueba();
        _historias.ExcepcionAlLeer = new TimeoutException("MongoDB no responde.");

        var detalle = await _useCase.EjecutarAsync(AdmisionId, CancellationToken.None);

        Assert.NotNull(detalle);
        Assert.Equal(25000m, detalle.ValorCopago);
        Assert.Null(detalle.HistoriaClinicaJson);
        Assert.False(detalle.TieneHistoriaClinica);
    }

    /// <summary>
    /// Escenario real mientras el Outbox aun no ha despachado: el documento
    /// todavia no existe en Mongo, pero la admision ya es consultable.
    /// </summary>
    [Fact]
    public async Task Una_admision_pendiente_de_sincronizar_se_consulta_sin_historia_clinica()
    {
        _consultas.Registro = RegistroDePrueba(estado: "PendienteSincronizacion");
        _historias.Contenido = null;

        var detalle = await _useCase.EjecutarAsync(AdmisionId, CancellationToken.None);

        Assert.NotNull(detalle);
        Assert.Equal("PendienteSincronizacion", detalle.Estado);
        Assert.False(detalle.TieneHistoriaClinica);
    }

    [Fact]
    public async Task Un_identificador_vacio_no_llega_a_consultar_la_base_de_datos()
    {
        _consultas.Registro = RegistroDePrueba();

        var detalle = await _useCase.EjecutarAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(detalle);
    }

    private static AdmisionRegistro RegistroDePrueba(string estado = "Sincronizada") =>
        new(AdmisionId,
            AdmisionId.ToString("N"),
            "CC",
            "1098765432",
            "Ana Maria Portilla",
            25000m,
            "COP",
            Ahora,
            estado,
            IntentosSincronizacion: 0,
            SincronizadaEnUtc: Ahora,
            MotivoFallo: null);
}
