using KIScheduler.Infrastructure;
using KIScheduler.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Infrastructure;

[TestClass]
public sealed class RuntimeAndLoggingTests
{
    [TestMethod]
    public void DefaultWritablePathsDoNotUseInstallationDirectory()
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), "protected-install");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var paths = RuntimePaths.FromConfiguration(configuration, installDirectory);

        Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KIScheduler"), paths.DataDirectory);
        Assert.AreEqual(Path.Combine(paths.DataDirectory, "data", "kischeduler.db"), paths.DatabasePath);
        Assert.AreEqual(Path.Combine(paths.DataDirectory, "logs"), paths.LogDirectory);
        Assert.IsFalse(paths.DatabasePath.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void PortableModeResolvesAllRelativePathsBelowApplicationDirectory()
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), "kischeduler-portable");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Runtime:DataDirectory"] = ".",
                ["Persistence:DatabasePath"] = "state/scheduler.db",
                ["Logging:File:Directory"] = "diagnostics"
            }).Build();

        var paths = RuntimePaths.FromConfiguration(configuration, installDirectory);

        Assert.AreEqual(Path.GetFullPath(installDirectory), paths.DataDirectory);
        Assert.AreEqual(Path.Combine(paths.DataDirectory, "state", "scheduler.db"), paths.DatabasePath);
        Assert.AreEqual(Path.Combine(paths.DataDirectory, "diagnostics"), paths.LogDirectory);
    }

    [TestMethod]
    public void FileLoggerRedactsHeadersStructuredSecretsAndExceptionMessages()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kischeduler-logs-{Guid.NewGuid():N}");
        try
        {
            using (var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions
            {
                Directory = directory,
                FileNamePrefix = "security-",
                RetainedFileCountLimit = 1
            }, directory))
            {
                var logger = provider.CreateLogger("security-test");
                logger.LogError(new InvalidOperationException("api_token=exception-secret"),
                    "Authorization: Bearer header-secret; payload {Payload}",
                    "{\"password\":\"json-secret\"}");
            }

            var contents = File.ReadAllText(Directory.EnumerateFiles(directory, "*.log").Single());
            Assert.IsFalse(contents.Contains("header-secret", StringComparison.Ordinal));
            Assert.IsFalse(contents.Contains("json-secret", StringComparison.Ordinal));
            Assert.IsFalse(contents.Contains("exception-secret", StringComparison.Ordinal));
            StringAssert.Contains(contents, "<redacted>");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
