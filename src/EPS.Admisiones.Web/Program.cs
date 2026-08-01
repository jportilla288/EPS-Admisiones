using EPS.Admisiones.Application;
using EPS.Admisiones.Infrastructure;
using EPS.Admisiones.Infrastructure.Persistence.SqlServer;
using EPS.Admisiones.Web.Components;
using EPS.Admisiones.Web.Utilities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Capas de la arquitectura hexagonal.
// El host solo compone: no conoce EF Core ni el driver de Mongo.
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Adaptador de entrada 1: API REST.
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ProblemDetails + manejador tipado: las violaciones de invariantes del dominio
// se traducen a 422, no a 500. Evita try/catch repetido en cada controlador.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ---------------------------------------------------------------------------
// Adaptador de entrada 2: Blazor Server.
//
// RESPUESTA A LA PREGUNTA 1 DE LA PARTE 1 (escalamiento horizontal):
//  a) Sesiones pegajosas (ARR Affinity) o, preferible, Azure SignalR Service en
//     modo servidor: descarga los WebSockets del App Service y elimina la
//     necesidad de afinidad. Con 5.000 usuarios concurrentes, es la diferencia
//     entre agotar los sockets del plan y no hacerlo.
//  b) El estado del circuito vive en memoria de UNA instancia. Todo estado que
//     deba sobrevivir a una reconexion va a un almacen externo (Redis), no a
//     campos del componente.
//  c) Los limites de abajo acotan cuanta memoria retiene el servidor por
//     circuitos desconectados: es la valvula que evita el efecto bola de nieve.
// ---------------------------------------------------------------------------
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();

        // Cuantos circuitos desconectados se retienen para permitir reconexion.
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);

        // Contrapresion: si el cliente no confirma los batches, se deja de
        // encolar en lugar de crecer sin limite en RAM.
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 64 * 1024;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// En desarrollo se aplican migraciones al arrancar para que
// "docker compose up + dotnet run" funcione sin pasos manuales.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AdmisionesDbContext>();

    // Si el repositorio trae migraciones, se aplican. Si aun no se ha generado
    // ninguna (`dotnet ef migrations add Inicial`), se crea el esquema a partir
    // del modelo para que un clon recien hecho arranque sin pasos manuales.
    if (db.Database.GetMigrations().Any())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
