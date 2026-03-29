using Microsoft.Extensions.DependencyInjection;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Application.Services;

namespace RegulatoryIntake.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMetadataValidationService, MetadataValidationService>();
        services.AddSingleton<IChecksumService, Sha256ChecksumService>();
        services.AddScoped<IIntakeOrchestrator, IntakeOrchestrator>();
        return services;
    }
}
