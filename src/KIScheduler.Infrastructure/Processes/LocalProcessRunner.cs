using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KIScheduler.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace KIScheduler.Infrastructure.Processes;

public sealed class LocalProcessRunner(ILogger<LocalProcessRunner> logger) : IProcessRunner
{
    private const string RedactedValue = "<redacted>";

    public async Task<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        Stopwatch stopwatch = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ProcessRunResult(
                ProcessTerminationReason.Cancelled,
                null,
                stopwatch.Elapsed,
                Array.Empty<ProcessOutputLine>());
        }

        ProcessStartInfo startInfo = CreateStartInfo(request);
        string redactedCommand = FormatCommandForLog(request);
        logger.LogInformation(
            "Starting local process {Command} in {WorkingDirectory}; environment overrides: {Environment}",
            redactedCommand,
            startInfo.WorkingDirectory,
            FormatEnvironmentForLog(request));

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return StartFailed("The operating system did not start the process.");
            }
        }
        catch (Exception exception) when (exception is
            Win32Exception or
            InvalidOperationException or
            FileNotFoundException or
            DirectoryNotFoundException or
            UnauthorizedAccessException)
        {
            logger.LogWarning("Could not start local process {Command}: {Error}", redactedCommand, exception.Message);
            return StartFailed(exception.Message);
        }

        var output = new ConcurrentQueue<ProcessOutputLine>();
        long sequence = 0;
        string[] secretValues = GetSecretValues(request);

        Task stdoutTask = ReadLinesAsync(
            process.StandardOutput,
            ProcessOutputStream.StandardOutput,
            output,
            () => Interlocked.Increment(ref sequence),
            secretValues,
            request.SuppressOutputLogging);
        Task stderrTask = ReadLinesAsync(
            process.StandardError,
            ProcessOutputStream.StandardError,
            output,
            () => Interlocked.Increment(ref sequence),
            secretValues,
            request.SuppressOutputLogging);
        Task stdinTask = WriteStandardInputAsync(process, request.StandardInput);
        Task exitTask = process.WaitForExitAsync(CancellationToken.None);

        Task timeoutTask = request.Timeout is { } timeout
            ? Task.Delay(timeout, CancellationToken.None)
            : Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
        Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        Task completedTask = await Task.WhenAny(exitTask, timeoutTask, cancellationTask).ConfigureAwait(false);
        ProcessTerminationReason reason;

        if (exitTask.IsCompleted)
        {
            reason = ProcessTerminationReason.Completed;
        }
        else if (cancellationToken.IsCancellationRequested || completedTask == cancellationTask)
        {
            reason = ProcessTerminationReason.Cancelled;
            logger.LogInformation("Cancelling local process {Command} and its process tree", redactedCommand);
            KillProcessTree(process, redactedCommand);
        }
        else
        {
            reason = ProcessTerminationReason.TimedOut;
            logger.LogWarning(
                "Local process {Command} exceeded its timeout of {Timeout} and its process tree will be stopped",
                redactedCommand,
                request.Timeout);
            KillProcessTree(process, redactedCommand);
        }

        await exitTask.ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask, stdinTask).ConfigureAwait(false);
        stopwatch.Stop();

        int exitCode = process.ExitCode;
        ProcessOutputLine[] orderedOutput = output.OrderBy(line => line.Sequence).ToArray();
        logger.LogInformation(
            "Local process {Command} ended after {Duration} with reason {Reason} and exit code {ExitCode}",
            redactedCommand,
            stopwatch.Elapsed,
            reason,
            exitCode);

        return new ProcessRunResult(reason, exitCode, stopwatch.Elapsed, orderedOutput);

        ProcessRunResult StartFailed(string error)
        {
            stopwatch.Stop();
            return new ProcessRunResult(
                ProcessTerminationReason.StartFailed,
                null,
                stopwatch.Elapsed,
                Array.Empty<ProcessOutputLine>(),
                error);
        }
    }

    private static void Validate(ProcessRunRequest request)
    {
        if (request.Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be greater than zero.");
        }

        if (request.Arguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Arguments must not contain null values.", nameof(request));
        }

        if (request.SensitiveArgumentIndexes.Any(index => index < 0 || index >= request.Arguments.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Sensitive argument indexes must refer to an existing argument.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRunRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in request.EnvironmentVariables)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private async Task ReadLinesAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        ConcurrentQueue<ProcessOutputLine> output,
        Func<long> nextSequence,
        IReadOnlyCollection<string> secretValues,
        bool suppressLogging)
    {
        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            var outputLine = new ProcessOutputLine(nextSequence(), stream, line, DateTimeOffset.UtcNow);
            output.Enqueue(outputLine);

            if (suppressLogging) continue;

            string safeLine = RedactKnownValues(line, secretValues);
            if (stream == ProcessOutputStream.StandardOutput)
            {
                logger.LogDebug("process stdout: {Line}", safeLine);
            }
            else
            {
                logger.LogDebug("process stderr: {Line}", safeLine);
            }
        }
    }

    private static async Task WriteStandardInputAsync(Process process, string? input)
    {
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            process.HasExited && exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            // A process is allowed to exit without consuming all input.
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private void KillProcessTree(Process process, string redactedCommand)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The process exited between the check and the kill request.
        }
        catch (Exception exception) when (exception is Win32Exception or NotSupportedException)
        {
            logger.LogError(exception, "Failed to stop process tree for {Command}", redactedCommand);
            throw;
        }
    }

    private static string FormatCommandForLog(ProcessRunRequest request)
    {
        HashSet<int> explicitSensitiveIndexes = request.SensitiveArgumentIndexes.ToHashSet();
        var safeArguments = new string[request.Arguments.Count];
        bool redactNext = false;

        for (var index = 0; index < request.Arguments.Count; index++)
        {
            string argument = request.Arguments[index];
            bool explicitlySensitive = explicitSensitiveIndexes.Contains(index);
            int separator = argument.IndexOf('=');
            bool sensitiveAssignment = separator > 0 && IsSensitiveName(argument[..separator]);

            if (explicitlySensitive || redactNext)
            {
                safeArguments[index] = RedactedValue;
            }
            else if (sensitiveAssignment)
            {
                safeArguments[index] = argument[..(separator + 1)] + RedactedValue;
            }
            else
            {
                safeArguments[index] = argument;
            }

            redactNext = separator < 0 && IsSensitiveName(argument);
        }

        return string.Join(' ', new[] { request.FileName }.Concat(safeArguments).Select(QuoteForDisplay));
    }

    private static string FormatEnvironmentForLog(ProcessRunRequest request)
    {
        if (request.EnvironmentVariables.Count == 0)
        {
            return "<none>";
        }

        // Environment values are not needed for diagnostics. Omitting all values is safer than
        // relying only on a list of well-known secret names.
        return string.Join(", ", request.EnvironmentVariables.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => $"{name}=<set>"));
    }

    private static string[] GetSecretValues(ProcessRunRequest request)
    {
        HashSet<int> indexes = request.SensitiveArgumentIndexes.ToHashSet();
        for (var index = 0; index < request.Arguments.Count; index++)
        {
            string argument = request.Arguments[index];
            int separator = argument.IndexOf('=');
            if (separator > 0 && IsSensitiveName(argument[..separator]))
            {
                indexes.Add(index);
            }
            else if (separator < 0 && IsSensitiveName(argument) && index + 1 < request.Arguments.Count)
            {
                indexes.Add(index + 1);
            }
        }

        HashSet<string> sensitiveEnvironmentNames = request.SensitiveEnvironmentVariableNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> argumentSecrets = indexes.Select(index => request.Arguments[index])
            .Select(value =>
            {
                int separator = value.IndexOf('=');
                return separator > 0 ? value[(separator + 1)..] : value;
            });
        IEnumerable<string> environmentSecrets = request.EnvironmentVariables
            .Where(pair => sensitiveEnvironmentNames.Contains(pair.Key) || IsSensitiveName(pair.Key))
            .Select(pair => pair.Value);

        return argumentSecrets.Concat(environmentSecrets)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    private static bool IsSensitiveName(string value)
    {
        string normalized = new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return normalized.Contains("PASSWORD", StringComparison.Ordinal)
            || normalized.Contains("PASSPHRASE", StringComparison.Ordinal)
            || normalized.Contains("TOKEN", StringComparison.Ordinal)
            || normalized.Contains("SECRET", StringComparison.Ordinal)
            || normalized.Contains("CREDENTIAL", StringComparison.Ordinal)
            || normalized.Contains("AUTHORIZATION", StringComparison.Ordinal)
            || normalized.Contains("APIKEY", StringComparison.Ordinal)
            || normalized.Contains("ACCESSKEY", StringComparison.Ordinal)
            || normalized.Contains("PRIVATEKEY", StringComparison.Ordinal);
    }

    private static string RedactKnownValues(string value, IEnumerable<string> secretValues)
    {
        foreach (string secretValue in secretValues)
        {
            value = value.Replace(secretValue, RedactedValue, StringComparison.Ordinal);
        }

        return value;
    }

    private static string QuoteForDisplay(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
