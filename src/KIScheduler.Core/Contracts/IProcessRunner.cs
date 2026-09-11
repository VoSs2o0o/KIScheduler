namespace KIScheduler.Core.Contracts;

/// <summary>
/// Runs a local executable without involving a command shell.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default);
}

public sealed record class ProcessRunRequest
{
    public ProcessRunRequest(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        FileName = fileName;
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    public string? WorkingDirectory { get; init; }

    public string? StandardInput { get; init; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Zero-based positions whose values must never be written to a log.
    /// Values of common secret switches such as --token are detected in addition.
    /// </summary>
    public IReadOnlyCollection<int> SensitiveArgumentIndexes { get; init; } = Array.Empty<int>();

    public IReadOnlyCollection<string> SensitiveEnvironmentVariableNames { get; init; }
        = Array.Empty<string>();

    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Captures output in the result but prevents raw stdout/stderr lines from being written to logs.
    /// Use this for tools whose structured output can contain credentials or sensitive source data.
    /// </summary>
    public bool SuppressOutputLogging { get; init; }
}

public enum ProcessTerminationReason
{
    Completed,
    StartFailed,
    TimedOut,
    Cancelled
}

public enum ProcessOutputStream
{
    StandardOutput,
    StandardError
}

public sealed record ProcessOutputLine(
    long Sequence,
    ProcessOutputStream Stream,
    string Text,
    DateTimeOffset ReceivedAt);

public sealed class ProcessRunResult
{
    public ProcessRunResult(
        ProcessTerminationReason terminationReason,
        int? exitCode,
        TimeSpan duration,
        IReadOnlyList<ProcessOutputLine> output,
        string? startError = null)
    {
        TerminationReason = terminationReason;
        ExitCode = exitCode;
        Duration = duration;
        Output = output;
        StartError = startError;
    }

    public ProcessTerminationReason TerminationReason { get; }

    public int? ExitCode { get; }

    public TimeSpan Duration { get; }

    public IReadOnlyList<ProcessOutputLine> Output { get; }

    public string? StartError { get; }

    public bool Succeeded => TerminationReason == ProcessTerminationReason.Completed && ExitCode == 0;

    public IReadOnlyList<string> StandardOutput => Output
        .Where(line => line.Stream == ProcessOutputStream.StandardOutput)
        .Select(line => line.Text)
        .ToArray();

    public IReadOnlyList<string> StandardError => Output
        .Where(line => line.Stream == ProcessOutputStream.StandardError)
        .Select(line => line.Text)
        .ToArray();
}
