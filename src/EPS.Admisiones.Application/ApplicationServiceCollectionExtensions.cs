using EPS.Admisiones.Application.UseCases.AdmitirPaciente;
using Microsoft.Extensions.DependencyInjection;

namespace EPS.Admisiones.Application;

/// <summary>
/// Registro de la capa Application. El host no necesita conocer los tipos
/// concretos de los casos de uso, solo llamar a este metodo.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdmitirPacienteUseCase, AdmitirPacienteUseCase>();
        services.AddSingleton<IExtractorDatosFacturables, ExtractorDatosFacturables>();

        return services;
    }
}
