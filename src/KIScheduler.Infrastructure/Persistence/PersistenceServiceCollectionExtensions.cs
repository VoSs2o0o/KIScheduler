using KIScheduler.Core.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KIScheduler.Core.Scheduling;

namespace KIScheduler.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddKischedulerPersistence(this IServiceCollection services,
        IConfiguration configuration, string contentRootPath)
    {
        var configuredPath = configuration["Persistence:DatabasePath"];
        var databasePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine("data", "kischeduler.db") : configuredPath, contentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = true
        }.ToString();

        services.AddDbContextFactory<KischedulerDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IWorkItemRepository, SqliteWorkItemRepository>();
        services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        services.AddSingleton<IPlatformRepository, SqlitePlatformRepository>();
        services.AddSingleton<IUsagePolicyRepository, SqliteUsagePolicyRepository>();
        services.AddSingleton<IUsageSnapshotRepository, SqliteUsageSnapshotRepository>();
        services.AddSingleton<IExecutionHistoryRepository, SqliteExecutionHistoryRepository>();
        services.AddSingleton<IExecutionBlockRepository, SqliteExecutionBlockRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IAtomicExecutionRepository, SqliteAtomicExecutionRepository>();
        services.AddSingleton<IStartupRecoveryRepository, SqliteStartupRecoveryRepository>();
        services.AddSingleton(new DatabaseWorkerLock(databasePath));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<UsagePolicyEvaluator>();
        services.AddSingleton<SchedulerPriorityCalculator>();
        // Hosted services start in registration order: lock the database before migrating or recovering it.
        services.AddHostedService<DatabaseWorkerLockHostedService>();
        services.AddHostedService<DatabaseInitializationService>();
        services.AddHostedService<StartupRecoveryHostedService>();
        return services;
    }
}
