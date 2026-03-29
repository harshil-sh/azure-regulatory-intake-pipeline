using Microsoft.Extensions.Hosting;
using RegulatoryIntake.Application.DependencyInjection;
using RegulatoryIntake.Infrastructure.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices();
    })
    .Build();

host.Run();
