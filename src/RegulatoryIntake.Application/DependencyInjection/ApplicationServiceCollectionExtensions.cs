using Microsoft.Extensions.DependencyInjection;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Application.Services;

namespace RegulatoryIntake.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IIntakeOrchestrator, IntakeOrchestrator>();
        return services;
    }
}
