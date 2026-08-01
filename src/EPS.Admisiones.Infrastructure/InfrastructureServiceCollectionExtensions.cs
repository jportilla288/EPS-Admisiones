using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Infrastructure.Messaging;
using EPS.Admisiones.Infrastructure.Outbox;
using EPS.Admisiones.Infrastructure.Persistence.MongoDb;
using EPS.Admisiones.Infrastructure.Persistence.SqlServer;
using EPS.Admisiones.Infrastructure.Persistence.SqlServer.Repositories;
using EPS.Admisiones.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Polly;

namespace EPS.Admisiones.Infrastructure;

/// <summary>
/// Composicion de los adaptadores. Es el UNICO lugar donde se decide que
/// implementacion concreta satisface cada puerto.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IRelojSistema, RelojSistema>();

        AgregarSqlServer(services, configuration);
        AgregarMongo(services, configuration);
        AgregarOutbox(services, configuration);
        AgregarMensajeria(services);

        return services;
    }

    private static void AgregarSqlServer(IServiceCollection services, IConfiguration configuration)
    {
        var cadena = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexion 'ConnectionStrings:SqlServer'.");

        services.AddDbContext<AdmisionesDbContext>(options =>
            options.UseSqlServer(cadena, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "admisiones");

                // RESPUESTA A LA PREGUNTA 3 DE LA PARTE 1:
                // ante una caida transitoria de SQL de ~5 segundos, EF Core
                // reintenta automaticamente con backoff exponencial usando la
                // lista de codigos de error transitorios de Azure SQL. La
                // peticion se ralentiza, pero la admision no se pierde.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);

                sql.CommandTimeout(30);
            }));

        services.AddScoped<IAdmisionRepository, AdmisionRepository>();
        services.AddScoped<IPacienteRepository, PacienteRepository>();
        services.AddScoped<IAdmisionesQuery, AdmisionesQuery>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AgregarMongo(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SeccionConfiguracion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // El driver de Mongo mantiene su propio pool de conexiones y es
        // thread-safe: debe ser SINGLETON. Crear un IMongoClient por peticion
        // es la causa mas comun de agotamiento de sockets en produccion.
        services.AddSingleton<IMongoClient>(proveedor =>
        {
            var opciones = configuration
                .GetSection(MongoOptions.SeccionConfiguracion)
                .Get<MongoOptions>()
                ?? throw new InvalidOperationException("Falta la seccion de configuracion 'Mongo'.");

            var ajustes = MongoClientSettings.FromConnectionString(opciones.ConnectionString);
            ajustes.MaxConnectionPoolSize = 200;
            ajustes.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            ajustes.RetryWrites = true;

            return new MongoClient(ajustes);
        });

        services.AddScoped<IHistoriaClinicaRepository, MongoHistoriaClinicaRepository>();
    }

    private static void AgregarOutbox(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SeccionConfiguracion));

        services.AddResiliencePipeline(
            PoliticasResiliencia.PipelineMongo,
            PoliticasResiliencia.ConfigurarPipelineMongo);

        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddHostedService<OutboxDispatcher>();
    }

    private static void AgregarMensajeria(IServiceCollection services)
    {
        // Una sola instancia compartida entre el BackgroundService y los
        // circuitos de Blazor: por eso se registra el singleton concreto y el
        // puerto se resuelve hacia el mismo objeto.
        services.AddSingleton<AdmisionEventBus>();
        services.AddSingleton<IAdmisionNotifier>(sp => sp.GetRequiredService<AdmisionEventBus>());
    }
}
