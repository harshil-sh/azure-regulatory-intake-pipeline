using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Infrastructure.Configuration;
using RegulatoryIntake.Infrastructure.Services;

namespace RegulatoryIntake.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        storageOptions.Validate();

        services.AddSingleton(Options.Create(storageOptions));
        services.AddSingleton(_ => new BlobServiceClient(storageOptions.ConnectionString));
        services.AddSingleton(_ => new QueueServiceClient(storageOptions.ConnectionString));
        services.AddSingleton(_ => new TableServiceClient(storageOptions.ConnectionString));

        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IQueueStorageService, AzureQueueStorageService>();
        services.AddScoped<ITableStorageService, AzureTableStorageService>();

        return services;
    }
}
