using KIScheduler.Infrastructure.Logging;
using KIScheduler.Infrastructure.Git;
using KIScheduler.Infrastructure.Persistence;
using KIScheduler.Infrastructure.Processes;
using KIScheduler.Infrastructure.Projects;
using KIScheduler.Platforms;
using KIScheduler.Core.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace KIScheduler.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using IHost host = CreateHostBuilder(args).Build();
        try
        {
            host.Start();
        }
        catch (DatabaseWorkerLockException exception)
        {
            MessageBox.Show("KIScheduler verwendet diese Datenbank bereits in einer anderen Instanz. " +
                "Die zweite Instanz wird beendet, damit kein Auftrag doppelt ausgeführt wird.\r\n\r\n" +
                exception.LockPath, "KIScheduler bereits aktiv", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
                AddPersistedSettings(configuration);
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
                services.AddKischedulerGit();
                services.AddKischedulerProjectServices();
                services.AddKischedulerPlatformServices();
                services.AddCodexPlatform(context.Configuration);
                services.AddClaudePlatform(context.Configuration);
                var schedulerOptions = new SchedulerOptions();
                context.Configuration.GetSection("Scheduler").Bind(schedulerOptions);
                schedulerOptions.Validate();
                services.AddSingleton(schedulerOptions);
                services.AddSingleton<ISchedulerEngine, SchedulerEngine>();
                services.AddSingleton<SchedulerUiService>();
                services.AddSingleton<MainForm>();
                services.AddHostedService<UiDefaultsInitializer>();
                services.AddHostedService<SchedulerWorker>();
            });

    private static void AddPersistedSettings(IConfigurationBuilder configuration)
    {
        try
        {
            var current = configuration.Build();
            var configuredPath = current["Persistence:DatabasePath"];
            var databasePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine("data", "kischeduler.db") : configuredPath, AppContext.BaseDirectory);
            if (!File.Exists(databasePath)) return;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Key, Value FROM Settings";
            using var reader = command.ExecuteReader();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                var key = reader.GetString(0).Replace('.', ':');
                var value = reader.GetString(1);
                if (key.Equals("Codex:AppServerArguments", StringComparison.OrdinalIgnoreCase))
                {
                    var arguments = JsonSerializer.Deserialize<string[]>(value) ?? [];
                    for (var i = 0; i < arguments.Length; i++) values[$"{key}:{i}"] = arguments[i];
                }
                else values[key] = value;
            }
            configuration.AddInMemoryCollection(values);
        }
        catch (SqliteException)
        {
            // On first start the database or Settings table does not exist yet.
        }
    }
}
