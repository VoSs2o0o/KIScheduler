using Microsoft.Extensions.Configuration;

namespace KIScheduler.Infrastructure;

/// <summary>
/// Resolves all writable application paths below one user-writable root. Relative database and
/// log paths are deliberately not resolved against the installation directory.
/// </summary>
public sealed record RuntimePaths(string DataDirectory, string DatabasePath, string LogDirectory)
{
    public static RuntimePaths FromConfiguration(IConfiguration configuration, string applicationDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        var dataDirectory = ResolveDataDirectory(configuration["Runtime:DataDirectory"], applicationDirectory);
        var databasePath = ResolveBelow(dataDirectory, configuration["Persistence:DatabasePath"],
            Path.Combine("data", "kischeduler.db"));
        var logDirectory = ResolveBelow(dataDirectory, configuration["Logging:File:Directory"], "logs");
        return new RuntimePaths(dataDirectory, databasePath, logDirectory);
    }

    public static string ResolveDataDirectory(string? configuredPath, string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded, applicationDirectory));
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
            throw new InvalidOperationException("Der benutzerspezifische Anwendungsdatenpfad ist nicht verfügbar.");
        return Path.Combine(localApplicationData, "KIScheduler");
    }

    private static string ResolveBelow(string dataDirectory, string? configuredPath, string fallback)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? fallback : configuredPath.Trim();
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(path, dataDirectory);
    }
}
