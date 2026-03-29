using Microsoft.Extensions.Hosting;
using RegulatoryIntake.Application.DependencyInjection;
using RegulatoryIntake.Infrastructure.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices(context.Configuration);
    })
    .Build();

host.Run();
