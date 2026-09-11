using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Claude;

public sealed class ClaudePlatform : IAiPlatform
{
    public static readonly PlatformId Id = new("claude");
    private readonly IProcessRunner processRunner;
    private readonly ClaudeOptions options;

    public ClaudePlatform(IProcessRunner processRunner, IOptions<ClaudeOptions> options)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public PlatformId PlatformId => Id;
    public PlatformCapabilities Capabilities { get; } = new(true, true, true);

    public async Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        ProcessRunResult result = await processRunner.RunAsync(new ProcessRunRequest(options.Executable)
        {
            Arguments = ["--version"],
            Timeout = options.AvailabilityTimeout
        }, cancellationToken).ConfigureAwait(false);

        if (result.TerminationReason == ProcessTerminationReason.StartFailed)
            return new PlatformHealth(PlatformHealthStatus.ExecutableMissing,
                $"Claude CLI konnte nicht gestartet werden: {result.StartError}");
        if (result.TerminationReason is ProcessTerminationReason.Cancelled or ProcessTerminationReason.TimedOut)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                "Die Versionsabfrage der Claude CLI wurde abgebrochen oder hat das Zeitlimit überschritten.");
        if (result.ExitCode != 0)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                FirstNonEmpty(result.StandardError, result.StandardOutput) ?? "Claude CLI meldete einen Fehler.");

        return new PlatformHealth(PlatformHealthStatus.Available,
            FirstNonEmpty(result.StandardOutput, result.StandardError) ?? "Claude CLI verfügbar.");
    }

    public async Task<PlatformExecutionResult> ExecuteAsync(PlatformExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.PlatformId.Value, Id.Value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Die Ausführungsanfrage gehört nicht zur Claude-Plattform.", nameof(request));

        ProcessRunResult process = await processRunner.RunAsync(new ProcessRunRequest(options.Executable)
        {
            Arguments = BuildArguments(request),
            WorkingDirectory = request.WorkingDirectory,
            StandardInput = request.Prompt,
            Timeout = request.Timeout,
            SuppressOutputLogging = true
        }, cancellationToken).ConfigureAwait(false);

        ClaudeJsonlParseResult parsed = ClaudeJsonlParser.Parse(process.StandardOutput);
        string stderr = string.Join(Environment.NewLine, process.StandardError);
        bool usageExceeded = parsed.UsageExceeded || ClaudeJsonlParser.ContainsUsageExceededText(stderr);
        bool authenticationError = parsed.AuthenticationError || ClaudeJsonlParser.ContainsAuthenticationText(stderr);
        bool networkError = parsed.NetworkError || ClaudeJsonlParser.ContainsNetworkText(stderr);
        string? diagnostic = parsed.ErrorMessage ?? FirstNonEmpty(process.StandardError) ?? parsed.ParseError;

        if (usageExceeded)
            return Result(PlatformExecutionOutcome.UsageExceeded, PlatformFailureKind.Quota,
                diagnostic ?? "Claude-Usage-Limit während der Ausführung erreicht.", true);

        return process.TerminationReason switch
        {
            ProcessTerminationReason.StartFailed => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.ProcessStart, process.StartError ?? "Claude CLI konnte nicht gestartet werden.", false),
            ProcessTerminationReason.Cancelled => Result(PlatformExecutionOutcome.Cancelled,
                PlatformFailureKind.None, "Claude-Ausführung wurde abgebrochen.", true),
            ProcessTerminationReason.TimedOut => Result(PlatformExecutionOutcome.TimedOut,
                PlatformFailureKind.None, "Claude-Ausführung hat das Zeitlimit überschritten.", true),
            _ when authenticationError => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Authentication, diagnostic ?? "Claude-Authentifizierung fehlgeschlagen.", true),
            _ when networkError => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Network, diagnostic ?? "Claude-Netzwerkverbindung fehlgeschlagen.", true),
            _ when parsed.ParseError is not null => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Parse, parsed.ParseError, true),
            _ when process.ExitCode == 0 => Result(PlatformExecutionOutcome.Succeeded,
                PlatformFailureKind.None, parsed.FinalMessage, false),
            _ => Result(PlatformExecutionOutcome.Failed, PlatformFailureKind.Unknown,
                diagnostic ?? $"Claude CLI wurde mit Exitcode {process.ExitCode} beendet.", true)
        };

        PlatformExecutionResult Result(PlatformExecutionOutcome outcome, PlatformFailureKind kind,
            string? message, bool partial) => new(outcome, process.ExitCode, parsed.SessionId ?? request.SessionId,
                message, partial, kind, parsed.Events);
    }

    private IReadOnlyList<string> BuildArguments(PlatformExecutionRequest request)
    {
        var arguments = new List<string>
        {
            "--print",
            "--output-format", "stream-json",
            "--verbose",
            "--model", request.ModelId.Value,
            "--effort", request.Effort.Value,
            "--permission-mode", options.PermissionMode
        };
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            arguments.Add("--resume");
            arguments.Add(request.SessionId);
        }
        arguments.AddRange(options.AdditionalArguments);
        return arguments;
    }

    private static string? FirstNonEmpty(params IReadOnlyList<string>[] groups) => groups
        .SelectMany(group => group)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
