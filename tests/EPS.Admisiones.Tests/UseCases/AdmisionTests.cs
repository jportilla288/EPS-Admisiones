using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Admisiones.Events;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;
using EPS.Admisiones.Domain.Exceptions;

namespace EPS.Admisiones.Tests.UseCases;

/// <summary>Invariantes del agregado, sin ningun tipo de infraestructura.</summary>
public sealed class AdmisionTests
{
    private static readonly DateTime Ahora = new(2026, 7, 31, 14, 30, 0, DateTimeKind.Utc);

    private static Admision CrearAdmision() => Admision.Registrar(
        Guid.NewGuid(),
        DocumentoPaciente.Crear("CC", "1098765432"),
        ValorCopago.Crear(25000m),
        Ahora);

    [Fact]
    public void Al_registrarse_emite_el_evento_PacienteAdmitido()
    {
        var admision = CrearAdmision();

        var evento = Assert.IsType<PacienteAdmitido>(Assert.Single(admision.EventosDeDominio));

        Assert.Equal(admision.Id, evento.AdmisionId);
        Assert.Equal(admision.HistoriaClinicaId, evento.HistoriaClinicaId);
    }

    [Fact]
    public void El_identificador_de_la_historia_clinica_se_deriva_del_id_de_la_admision()
    {
        // Que sea derivado (y no generado por Mongo) es lo que hace idempotente
        // el upsert del despachador.
        var admision = CrearAdmision();

        Assert.Equal(admision.Id.ToString("N"), admision.HistoriaClinicaId);
    }

    [Fact]
    public void Rechaza_una_fecha_que_no_venga_en_UTC()
    {
        var error = Assert.Throws<DomainException>(() => Admision.Registrar(
            Guid.NewGuid(),
            DocumentoPaciente.Crear("CC", "91234567"),
            ValorCopago.Crear(1000m),
            new DateTime(2026, 7, 31, 9, 30, 0, DateTimeKind.Local)));

        Assert.Contains("UTC", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmar_la_sincronizacion_dos_veces_es_idempotente()
    {
        var admision = CrearAdmision();
        var primera = Ahora.AddSeconds(3);

        admision.ConfirmarSincronizacion(primera);
        admision.ConfirmarSincronizacion(Ahora.AddMinutes(10));

        Assert.Equal(EstadoAdmision.Sincronizada, admision.Estado);
        Assert.Equal(primera, admision.SincronizadaEnUtc);
    }

    [Fact]
    public void Tras_agotar_los_reintentos_queda_marcada_para_conciliacion_manual()
    {
        var admision = CrearAdmision();

        for (var intento = 0; intento < 3; intento++)
        {
            admision.RegistrarFalloSincronizacion("Mongo no disponible.", maximoIntentos: 3);
        }

        Assert.Equal(EstadoAdmision.FallidaSincronizacion, admision.Estado);
        Assert.Equal(3, admision.IntentosSincronizacion);

        // Lo importante: la admision NO se perdio y el copago sigue bloqueado.
        Assert.Equal(25000m, admision.Copago.Monto);
    }

    [Fact]
    public void Dos_value_objects_con_el_mismo_valor_son_iguales()
    {
        Assert.Equal(DocumentoPaciente.Crear("CC", "91234567"), DocumentoPaciente.Crear("cc", "91234567"));
        Assert.Equal(ValorCopago.Crear(1000m), ValorCopago.Crear(1000m, "cop"));
    }
}
