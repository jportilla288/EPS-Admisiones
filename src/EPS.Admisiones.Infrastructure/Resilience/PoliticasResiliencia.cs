using MongoDB.Driver;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace EPS.Admisiones.Infrastructure.Resilience;

/// <summary>
/// Politicas de Polly v8 para las llamadas al almacen documental.
///
/// Reparto de responsabilidades frente a fallos transitorios:
///  - SQL Server: lo cubre EF Core con <c>EnableRetryOnFailure</c>, que ya
///    conoce los codigos de error transitorios de Azure SQL. Envolverlo ademas
///    con Polly duplicaria los reintentos.
///  - MongoDB: no trae reintentos de escritura equivalentes, asi que aqui si
///    se aplica timeout + retry + circuit breaker.
/// </summary>
public static class PoliticasResiliencia
{
    public const string PipelineMongo = "mongo-escritura";

    public static void ConfigurarPipelineMongo(ResiliencePipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            // Timeout por intento: evita que un socket colgado bloquee el worker.
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<MongoConnectionException>()
                    .Handle<MongoNodeIsRecoveringException>()
                    .Handle<MongoNotPrimaryException>()
                    .Handle<TimeoutRejectedException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                // Jitter: con 5.000 usuarios concurrentes, reintentos sincronizados
                // producirian picos de carga en el mismo instante.
                UseJitter = true
            })
            // Si Mongo esta caido, se deja de martillarlo: los mensajes quedan
            // en el Outbox y se procesan cuando el circuito vuelve a cerrarse.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<MongoConnectionException>()
                    .Handle<TimeoutRejectedException>(),
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15)
            });
    }
}
