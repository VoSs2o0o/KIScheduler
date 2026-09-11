using System.Collections.Concurrent;
using System.Diagnostics;
using KIScheduler.Core.Contracts;
using KIScheduler.Infrastructure.Processes;
using KIScheduler.ProcessTestHelper;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Processes;

[TestClass]
public sealed class LocalProcessRunnerTests
{
    private static string HelperAssemblyPath => typeof(Marker).Assembly.Location;

    [TestMethod]
    public async Task ArgumentsWorkingDirectoryAndUtf8InputArePreservedExactly()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), $"KI Scheduler {Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var request = HelperRequest("echo", "with spaces", "quotes \"stay\"", "äöü-東京") with
            {
                WorkingDirectory = workingDirectory,
                StandardInput = "Grüße aus Köln 👋"
            };

            ProcessRunResult result = await CreateRunner().RunAsync(request);

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.Contains(result.StandardOutput.ToList(), $"cwd:{workingDirectory}");
            CollectionAssert.Contains(result.StandardOutput.ToList(), "arg:0:with spaces");
            CollectionAssert.Contains(result.StandardOutput.ToList(), "arg:1:quotes \"stay\"");
            CollectionAssert.Contains(result.StandardOutput.ToList(), "arg:2:äöü-東京");
            CollectionAssert.Contains(result.StandardOutput.ToList(), "stdin:Grüße aus Köln 👋");
        }
        finally
        {
            Directory.Delete(workingDirectory);
        }
    }

    [TestMethod]
    public async Task LargeParallelOutputIsReadWithoutDeadlockOrLoss()
    {
        ProcessRunResult result = await CreateRunner().RunAsync(HelperRequest("flood", "3000") with
        {
            Timeout = TimeSpan.FromSeconds(20)
        });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(3000, result.StandardOutput.Count);
        Assert.AreEqual(3000, result.StandardError.Count);
        Assert.AreEqual(6000, result.Output.Count);
    }

    [TestMethod]
    public async Task EnvironmentOverridesArePassedToTheProcess()
    {
        string variableName = $"KISCHEDULER_TEST_{Guid.NewGuid():N}";
        var request = HelperRequest("environment", variableName) with
        {
            EnvironmentVariables = new Dictionary<string, string> { [variableName] = "value with ünicode" }
        };

        ProcessRunResult result = await CreateRunner().RunAsync(request);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(new[] { "value with ünicode" }, result.StandardOutput.ToArray());
    }

    [TestMethod]
    public async Task NonZeroExitCodeIsACompletedButUnsuccessfulRun()
    {
        ProcessRunResult result = await CreateRunner().RunAsync(HelperRequest("exit", "23"));

        Assert.AreEqual(ProcessTerminationReason.Completed, result.TerminationReason);
        Assert.AreEqual(23, result.ExitCode);
        Assert.IsFalse(result.Succeeded);
        CollectionAssert.Contains(result.StandardError.ToList(), "requested error output");
    }

    [TestMethod]
    public async Task MissingExecutableIsReportedAsStartFailure()
    {
        var request = new ProcessRunRequest($"missing-{Guid.NewGuid():N}.exe");

        ProcessRunResult result = await CreateRunner().RunAsync(request);

        Assert.AreEqual(ProcessTerminationReason.StartFailed, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StartError));
    }

    [TestMethod]
    public async Task ExternalCancellationIsDistinctFromTimeout()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        ProcessRunResult result = await CreateRunner().RunAsync(
            HelperRequest("sleep", "30000") with { Timeout = TimeSpan.FromSeconds(20) },
            cancellation.Token);

        Assert.AreEqual(ProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.IsTrue(result.Duration < TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task TimeoutStopsTheWholeChildProcessTree()
    {
        string markerPath = Path.Combine(Path.GetTempPath(), $"kischeduler-child-{Guid.NewGuid():N}.txt");
        try
        {
            ProcessRunResult result = await CreateRunner().RunAsync(HelperRequest("spawn-child", markerPath) with
            {
                Timeout = TimeSpan.FromSeconds(2)
            });

            Assert.AreEqual(ProcessTerminationReason.TimedOut, result.TerminationReason);
            Assert.IsTrue(File.Exists(markerPath), "The child helper did not start before the timeout.");
            int childId = int.Parse(await File.ReadAllTextAsync(markerPath),
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsTrue(await WaitUntilProcessHasExitedAsync(childId),
                $"Child process {childId} is still running.");
        }
        finally
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
    }

    [TestMethod]
    public async Task ExternalCancellationStopsTheWholeChildProcessTree()
    {
        string markerPath = Path.Combine(Path.GetTempPath(), $"kischeduler-child-{Guid.NewGuid():N}.txt");
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            ProcessRunResult result = await CreateRunner().RunAsync(
                HelperRequest("spawn-child", markerPath),
                cancellation.Token);

            Assert.AreEqual(ProcessTerminationReason.Cancelled, result.TerminationReason);
            Assert.IsTrue(File.Exists(markerPath), "The child helper did not start before cancellation.");
            int childId = int.Parse(await File.ReadAllTextAsync(markerPath),
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsTrue(await WaitUntilProcessHasExitedAsync(childId),
                $"Child process {childId} is still running.");
        }
        finally
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
    }

    [TestMethod]
    public async Task SensitiveArgumentsAndEnvironmentValuesAreRedactedFromLogs()
    {
        var logger = new RecordingLogger<LocalProcessRunner>();
        const string explicitSecret = "explicit-secret-123";
        const string detectedSecret = "detected-secret-456";
        const string environmentSecret = "environment-secret-789";
        var request = HelperRequest("echo", "--credential", explicitSecret, $"--api-token={detectedSecret}") with
        {
            SensitiveArgumentIndexes = new[] { 3 },
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["SERVICE_TOKEN"] = environmentSecret,
                ["VISIBLE_SETTING"] = "ordinary-value"
            }
        };

        ProcessRunResult result = await new LocalProcessRunner(logger).RunAsync(request);

        Assert.IsTrue(result.Succeeded);
        string logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.IsFalse(logs.Contains(explicitSecret, StringComparison.Ordinal));
        Assert.IsFalse(logs.Contains(detectedSecret, StringComparison.Ordinal));
        Assert.IsFalse(logs.Contains(environmentSecret, StringComparison.Ordinal));
        Assert.IsFalse(logs.Contains("ordinary-value", StringComparison.Ordinal));
        Assert.IsTrue(logs.Contains("<redacted>", StringComparison.Ordinal));
    }

    private static ProcessRunRequest HelperRequest(params string[] arguments) => new("dotnet")
    {
        Arguments = new[] { HelperAssemblyPath }.Concat(arguments).ToArray()
    };

    private static LocalProcessRunner CreateRunner() => new(new RecordingLogger<LocalProcessRunner>());

    private static async Task<bool> WaitUntilProcessHasExitedAsync(int processId)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
    }
}
