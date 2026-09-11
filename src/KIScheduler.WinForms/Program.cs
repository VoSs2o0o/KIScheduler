using KIScheduler.Infrastructure.Logging;
using KIScheduler.Infrastructure.Persistence;
using KIScheduler.Infrastructure.Processes;
using KIScheduler.Platforms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KIScheduler.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using IHost host = CreateHostBuilder(args).Build();
        host.Start();

        try
        {
            Application.Run(host.Services.GetRequiredService<MainForm>());
        }
        finally
        {
            host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddProvider(new RollingFileLoggerProvider(
                    RollingFileLoggerOptions.FromConfiguration(context.Configuration),
                    context.HostingEnvironment.ContentRootPath));
            })
            .ConfigureServices((context, services) =>
            {
                services.AddKischedulerPersistence(
                    context.Configuration,
                    context.HostingEnvironment.ContentRootPath);
                services.AddKischedulerProcessRunner();
                services.AddKischedulerPlatformServices();
                services.AddSingleton<MainForm>();
                services.AddHostedService<SchedulerWorker>();
            });
}
