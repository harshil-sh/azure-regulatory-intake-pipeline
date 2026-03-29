using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RegulatoryIntake.EventPublisher.Configuration;
using RegulatoryIntake.EventPublisher.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var publisherOptions = builder.Configuration
    .GetSection(PublisherOptions.SectionName)
    .Get<PublisherOptions>() ?? new PublisherOptions();
publisherOptions.Validate();

builder.Services.AddSingleton(Options.Create(publisherOptions));
builder.Services.AddSingleton(_ => new BlobServiceClient(publisherOptions.Storage.ConnectionString));
builder.Services.AddHttpClient<LocalConsolePublisher>();

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<LocalConsolePublisher>();
return await publisher.RunAsync(CancellationToken.None);
