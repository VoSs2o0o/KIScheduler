using Microsoft.Extensions.Configuration;

namespace KIScheduler.Infrastructure.Logging;

public sealed class RollingFileLoggerOptions
{
    public string Directory { get; init; } = "logs";

    public string FileNamePrefix { get; init; } = "kischeduler-";

    public int RetainedFileCountLimit { get; init; } = 14;

    public static RollingFileLoggerOptions FromConfiguration(IConfiguration configuration)
    {
        RollingFileLoggerOptions options = new();
        configuration.GetSection("Logging:File").Bind(options);

        if (string.IsNullOrWhiteSpace(options.Directory))
        {
            throw new InvalidOperationException("Logging:File:Directory must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.FileNamePrefix))
        {
            throw new InvalidOperationException("Logging:File:FileNamePrefix must not be empty.");
        }

        if (options.RetainedFileCountLimit < 1)
        {
            throw new InvalidOperationException("Logging:File:RetainedFileCountLimit must be at least 1.");
        }

        return options;
    }
}
