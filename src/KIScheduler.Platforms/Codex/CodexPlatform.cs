using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Codex;

public sealed class CodexPlatform : IAiPlatform
{
    public static readonly PlatformId Id = new("codex");
    private readonly IProcessRunner processRunner;
    private readonly CodexOptions options;
    private readonly IPlatformRepository? platformRepository;
    private readonly ICodexProfileResolver profileResolver;

    public CodexPlatform(IProcessRunner processRunner, IOptions<CodexOptions> options,
        ICodexProfileResolver profileResolver, IPlatformRepository? platformRepository = null)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.platformRepository = platformRepository;
        this.profileResolver = profileResolver ?? throw new ArgumentNullException(nameof(profileResolver));
        this.options.Validate();
    }

    public PlatformId PlatformId => Id;
    public PlatformCapabilities Capabilities { get; } = new(true, true, true);

    public async Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        PlatformProfile profile;
        try
        {
            profile = await profileResolver.ResolveDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (CodexProfileException exception)
        {
            return new PlatformHealth(PlatformHealthStatus.Misconfigured, exception.Message);
        }
        return await CheckAvailabilityAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformHealth> CheckAvailabilityAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        PlatformProfile profile;
        try
        {
            profile = await profileResolver.ResolveAsync(profileId, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexProfileException exception)
        {
            return new PlatformHealth(PlatformHealthStatus.Misconfigured, exception.Message);
        }
        return await CheckAvailabilityAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlatformHealth> CheckAvailabilityAsync(PlatformProfile profile,
        CancellationToken cancellationToken)
    {
        var executable = await ResolveExecutableAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> environment = CodexProcessEnvironment.ForProfile(
            executable, profile.ConfigurationDirectory);
        ProcessRunResult result = await processRunner.RunAsync(new ProcessRunRequest(executable)
        {
            Arguments = ["--version"],
            EnvironmentVariables = environment,
            Timeout = options.AvailabilityTimeout
        }, cancellationToken).ConfigureAwait(false);

        if (result.TerminationReason == ProcessTerminationReason.StartFailed)
            return new PlatformHealth(PlatformHealthStatus.ExecutableMissing,
                $"Codex CLI konnte nicht gestartet werden: {result.StartError}");
        if (result.TerminationReason is ProcessTerminationReason.Cancelled or ProcessTerminationReason.TimedOut)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                "Die Versionsabfrage der Codex CLI wurde abgebrochen oder hat das Zeitlimit überschritten.");
        if (result.ExitCode != 0)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                FirstNonEmpty(result.StandardError, result.StandardOutput) ?? "Codex CLI meldete einen Fehler.");

        string version = FirstNonEmpty(result.StandardOutput, result.StandardError) ?? "Codex CLI verfügbar.";
        ProcessRunResult login = await processRunner.RunAsync(new ProcessRunRequest(executable)
        {
            Arguments = ["login", "status"],
            EnvironmentVariables = environment,
            Timeout = options.AvailabilityTimeout,
            SuppressOutputLogging = true
        }, cancellationToken).ConfigureAwait(false);
        if (login.TerminationReason == ProcessTerminationReason.StartFailed)
            return new PlatformHealth(PlatformHealthStatus.ExecutableMissing,
                $"Codex CLI konnte für das Profil '{profile.DisplayName}' nicht gestartet werden: {login.StartError}");
        if (login.TerminationReason is ProcessTerminationReason.Cancelled or ProcessTerminationReason.TimedOut)
            return new PlatformHealth(PlatformHealthStatus.Unavailable,
                $"Die Anmeldeprüfung des Codex-Profils '{profile.DisplayName}' wurde abgebrochen oder hat das Zeitlimit überschritten.");
        if (login.ExitCode != 0)
            return new PlatformHealth(PlatformHealthStatus.Misconfigured,
                $"Das Codex-Profil '{profile.DisplayName}' enthält keine gültige CLI-Anmeldung. " +
                "Getrennte Profile benötigen den dateibasierten Credential-Store im jeweiligen CODEX_HOME.");

        return new PlatformHealth(PlatformHealthStatus.Available, version);
    }

    public async Task<PlatformExecutionResult> ExecuteAsync(PlatformExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.PlatformId.Value, Id.Value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Die Ausführungsanfrage gehört nicht zur Codex-Plattform.", nameof(request));

        PlatformProfile profile;
        try
        {
            profile = await profileResolver.ResolveAsync(request.PlatformProfileId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CodexProfileException exception)
        {
            return new PlatformExecutionResult(PlatformExecutionOutcome.HumanReviewRequired,
                message: exception.Message, failureKind: PlatformFailureKind.Authentication);
        }

        var executable = await ResolveExecutableAsync(cancellationToken).ConfigureAwait(false);
        ProcessRunResult processResult = await processRunner.RunAsync(new ProcessRunRequest(executable)
        {
            Arguments = BuildArguments(request),
            EnvironmentVariables = CodexProcessEnvironment.ForProfile(executable, profile.ConfigurationDirectory),
            WorkingDirectory = request.WorkingDirectory,
            StandardInput = request.Prompt,
            Timeout = request.Timeout,
            SuppressOutputLogging = true
        }, cancellationToken).ConfigureAwait(false);

        CodexJsonlParseResult parsed = CodexJsonlParser.Parse(processResult.StandardOutput);
        string stderr = string.Join(Environment.NewLine, processResult.StandardError);
        bool usageExceeded = parsed.UsageExceeded || CodexJsonlParser.ContainsUsageExceededText(stderr);
        bool authError = parsed.AuthenticationError || CodexJsonlParser.ContainsAuthenticationText(stderr);
        bool networkError = parsed.NetworkError || CodexJsonlParser.ContainsNetworkText(stderr);
        string? diagnostic = parsed.ErrorMessage ?? FirstNonEmpty(processResult.StandardError) ?? parsed.ParseError;

        if (usageExceeded)
            return Result(PlatformExecutionOutcome.UsageExceeded, PlatformFailureKind.Quota,
                diagnostic ?? "Codex-Usage-Limit während der Ausführung erreicht.", true);

        return processResult.TerminationReason switch
        {
            ProcessTerminationReason.StartFailed => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.ProcessStart, processResult.StartError ?? "Codex CLI konnte nicht gestartet werden.", false),
            ProcessTerminationReason.Cancelled => Result(PlatformExecutionOutcome.Cancelled,
                PlatformFailureKind.None, "Codex-Ausführung wurde abgebrochen.", true),
            ProcessTerminationReason.TimedOut => Result(PlatformExecutionOutcome.TimedOut,
                PlatformFailureKind.None, "Codex-Ausführung hat das Zeitlimit überschritten.", true),
            _ when authError => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Authentication, diagnostic ?? "Codex-Authentifizierung fehlgeschlagen.", true),
            _ when networkError => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Network, diagnostic ?? "Codex-Netzwerkverbindung fehlgeschlagen.", true),
            _ when parsed.ParseError is not null => Result(PlatformExecutionOutcome.HumanReviewRequired,
                PlatformFailureKind.Parse, parsed.ParseError, true),
            _ when processResult.ExitCode == 0 => Result(PlatformExecutionOutcome.Succeeded,
                PlatformFailureKind.None, parsed.FinalMessage, false),
            _ => Result(PlatformExecutionOutcome.Failed, PlatformFailureKind.Unknown,
                diagnostic ?? $"Codex CLI wurde mit Exitcode {processResult.ExitCode} beendet.", true)
        };

        PlatformExecutionResult Result(PlatformExecutionOutcome outcome, PlatformFailureKind kind,
            string? message, bool partial) => new(outcome, processResult.ExitCode, parsed.SessionId ?? request.SessionId,
                message, partial, kind, parsed.Events);
    }

    private IReadOnlyList<string> BuildArguments(PlatformExecutionRequest request)
    {
        var arguments = new List<string>
        {
            "exec",
            "--json",
            "--model", request.ModelId.Value,
            "--config", $"model_reasoning_effort=\"{EscapeToml(request.Effort.Value)}\"",
            "--sandbox", options.Sandbox,
            "--cd", request.WorkingDirectory
        };

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            arguments.Add("-");
        }
        else
        {
            arguments.Add("resume");
            arguments.Add(request.SessionId);
            arguments.Add("-");
        }

        return arguments;
    }

    private async Task<string> ResolveExecutableAsync(CancellationToken cancellationToken) =>
        platformRepository is null
            ? options.Executable
            : (await platformRepository.GetAsync(Id, cancellationToken).ConfigureAwait(false))?.Executable
                ?? options.Executable;

    private static string EscapeToml(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? FirstNonEmpty(params IReadOnlyList<string>[] groups) => groups
        .SelectMany(group => group)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
