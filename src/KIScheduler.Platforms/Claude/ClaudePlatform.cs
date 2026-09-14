using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Claude;

public sealed class ClaudePlatform : IAiPlatform
{
    public static readonly PlatformId Id = new("claude");
    private readonly IProcessRunner processRunner;
    private readonly ClaudeOptions options;
    private readonly IClaudeProfileResolver? profileResolver;
    private readonly IPlatformRepository? platformRepository;

    public ClaudePlatform(IProcessRunner processRunner, IOptions<ClaudeOptions> options,
        IClaudeProfileResolver? profileResolver = null, IPlatformRepository? platformRepository = null)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.profileResolver = profileResolver;
        this.platformRepository = platformRepository;
        this.options.Validate();
    }

    public PlatformId PlatformId => Id;
    public PlatformCapabilities Capabilities { get; } = new(true, true, true);

    public async Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (profileResolver is null)
            return await CheckAvailabilityLegacyAsync(cancellationToken).ConfigureAwait(false);

        PlatformProfile profile;
        try
        {
            profile = await profileResolver.ResolveDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ClaudeProfileException exception)
        {
            return new PlatformHealth(PlatformHealthStatus.Misconfigured, exception.Message);
        }
        return await CheckAvailabilityAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformHealth> CheckAvailabilityAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        if (profileResolver is null)
            return await CheckAvailabilityLegacyAsync(cancellationToken).ConfigureAwait(false);

        PlatformProfile profile;
        try
        {
            profile = await profileResolver.ResolveAsync(profileId, cancellationToken).ConfigureAwait(false);
        }
        catch (ClaudeProfileException exception)
        {
            return new PlatformHealth(PlatformHealthStatus.Misconfigured, exception.Message);
        }
        return await CheckAvailabilityAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlatformHealth> CheckAvailabilityAsync(PlatformProfile profile,
        CancellationToken cancellationToken)
    {
        var executable = await ResolveExecutableAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> environment =
            ClaudeProcessEnvironment.ForProfile(profile.ConfigurationDirectory);
        ProcessRunResult result = await processRunner.RunAsync(new ProcessRunRequest(
            executable)
        {
            Arguments = ["--version"],
            EnvironmentVariables = environment,
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

        ProcessRunResult login = await processRunner.RunAsync(new ProcessRunRequest(executable)
        {
            Arguments = ["auth", "status"],
            EnvironmentVariables = environment,
            Timeout = options.AvailabilityTimeout,
            SuppressOutputLogging = true
        }, cancellationToken).ConfigureAwait(false);
        if (login.TerminationReason == ProcessTerminationReason.StartFailed)
            return new PlatformHealth(PlatformHealthStatus.ExecutableMissing,
                $"Claude CLI konnte für das Profil '{profile.DisplayName}' nicht gestartet werden: {login.StartError}");
        if (login.TerminationReason is ProcessTerminationReason.Cancelled or ProcessTerminationReason.TimedOut)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                $"Die Anmeldeprüfung des Claude-Profils '{profile.DisplayName}' wurde abgebrochen oder hat das Zeitlimit überschritten.");
        if (login.ExitCode != 0)
            return new PlatformHealth(PlatformHealthStatus.Misconfigured,
                $"Das Claude-Profil '{profile.DisplayName}' enthält keine gültige CLI-Anmeldung. " +
                "Bitte das Profil mit 'claude auth login' anmelden.");

        return new PlatformHealth(PlatformHealthStatus.Available,
            FirstNonEmpty(result.StandardOutput, result.StandardError) ?? "Claude CLI verfügbar.");
    }

    public async Task<PlatformExecutionResult> ExecuteAsync(PlatformExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.PlatformId.Value, Id.Value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Die Ausführungsanfrage gehört nicht zur Claude-Plattform.", nameof(request));

        PlatformProfile? profile = null;
        if (profileResolver is not null)
        {
            try
            {
                profile = await profileResolver.ResolveAsync(request.PlatformProfileId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ClaudeProfileException exception)
            {
                return new PlatformExecutionResult(PlatformExecutionOutcome.HumanReviewRequired,
                    message: exception.Message, failureKind: PlatformFailureKind.Authentication);
            }
        }

        var executable = await ResolveExecutableAsync(cancellationToken).ConfigureAwait(false);
        ProcessRunResult process = await processRunner.RunAsync(new ProcessRunRequest(
            executable)
        {
            Arguments = BuildArguments(request),
            EnvironmentVariables = profile is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ClaudeProcessEnvironment.ForProfile(profile.ConfigurationDirectory),
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

    private async Task<string> ResolveExecutableAsync(CancellationToken cancellationToken) =>
        platformRepository is null
            ? options.Executable
            : (await platformRepository.GetAsync(Id, cancellationToken).ConfigureAwait(false))?.Executable
                ?? options.Executable;

    private async Task<PlatformHealth> CheckAvailabilityLegacyAsync(CancellationToken cancellationToken)
    {
        ProcessRunResult result = await processRunner.RunAsync(new ProcessRunRequest(
            await ResolveExecutableAsync(cancellationToken).ConfigureAwait(false))
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

    private static string? FirstNonEmpty(params IReadOnlyList<string>[] groups) => groups
        .SelectMany(group => group)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
