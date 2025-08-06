using DockerBackup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<BackupServiceOptions>(options =>
        {
            context.Configuration.GetSection("Schedule").Bind(options);
        });
        
        services.AddSingleton<DockerClient>();
        services.AddHostedService<BackupService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .Build();

await host.RunAsync();
