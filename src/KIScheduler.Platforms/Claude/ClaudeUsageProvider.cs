using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace KIScheduler.Platforms.Claude;

public sealed class ClaudeUsageProvider : IUsageProvider
{
    private readonly CommandRegexReader reader;
    private readonly IClock clock;
    private readonly ClaudeOptions options;
    private readonly IClaudeProfileResolver? profileResolver;
    private readonly IPlatformRepository? platformRepository;
    private readonly PlatformProfileId? directProfileId;

    public ClaudeUsageProvider(CommandRegexReader reader, IClock clock, IOptions<ClaudeOptions> options,
        IPlatformRepository? platformRepository = null, PlatformProfileId? platformProfileId = null)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.platformRepository = platformRepository;
        directProfileId = platformProfileId;
        this.options.Validate();
    }

    public ClaudeUsageProvider(CommandRegexReader reader, IClaudeProfileResolver profileResolver,
        IClock clock, IOptions<ClaudeOptions> options, IPlatformRepository? platformRepository = null)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.profileResolver = profileResolver ?? throw new ArgumentNullException(nameof(profileResolver));
        this.platformRepository = platformRepository;
        this.options.Validate();
    }

    public PlatformId PlatformId => ClaudePlatform.Id;
    public UsageProviderCapabilities Capabilities => new(false, true,
        options.Usage.ConfiguredResetAtUtc.HasValue || !string.IsNullOrWhiteSpace(options.Usage.ResetGroupName));

    public event EventHandler<UsageChangedEventArgs>? UsageChanged
    {
        add { }
        remove { }
    }

    public async Task<UsageReadResult> ReadAsync(bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        _ = forceRefresh; // This polling provider intentionally does not cache reads.
        if (profileResolver is not null)
        {
            try
            {
                PlatformProfile profile = await profileResolver.ResolveDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                return await ReadCoreAsync(profile, profile.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (ClaudeProfileException exception)
            {
                return UsageReadResult.Unknown($"Claude-Usage unbekannt (Profil): {exception.Message}");
            }
        }

        if (directProfileId is not { } profileId)
            return UsageReadResult.Unknown("Claude-Usage unbekannt: Es ist keine Profil-ID konfiguriert.");
        return await ReadCoreAsync(null, profileId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UsageReadResult> ReadAsync(PlatformProfileId profileId, bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        _ = forceRefresh; // This polling provider intentionally does not cache reads.
        if (profileResolver is null)
            throw new InvalidOperationException("Dieser Claude-Usage-Provider besitzt keinen Profil-Resolver.");

        try
        {
            PlatformProfile profile = await profileResolver.ResolveAsync(profileId, cancellationToken)
                .ConfigureAwait(false);
            return await ReadCoreAsync(profile, profileId, cancellationToken).ConfigureAwait(false);
        }
        catch (ClaudeProfileException exception)
        {
            return UsageReadResult.Unknown($"Claude-Usage unbekannt (Profil): {exception.Message}")
                .ForProfile(profileId);
        }
    }

    private async Task<UsageReadResult> ReadCoreAsync(PlatformProfile? profile, PlatformProfileId profileId,
        CancellationToken cancellationToken)
    {
        var executable = platformRepository is null
            ? options.Usage.Executable
            : (await platformRepository.GetAsync(ClaudePlatform.Id, cancellationToken).ConfigureAwait(false))?.Executable
                ?? options.Usage.Executable;
        CommandRegexReadResult read = await reader.ReadAsync(CreateRequest(executable, profile), cancellationToken)
            .ConfigureAwait(false);

        if (read.Process.TerminationReason == ProcessTerminationReason.StartFailed)
            return WithProfile(new UsageReadResult(UsageReadStatus.ProviderUnavailable, message:
                $"Claude-Usage-Befehl konnte nicht gestartet werden: {read.Process.StartError}"), profileId);
        if (read.Process.TerminationReason == ProcessTerminationReason.Cancelled)
            return WithProfile(UsageReadResult.Unknown("Claude-Usage-Abfrage wurde abgebrochen."), profileId);
        if (read.Process.TerminationReason == ProcessTerminationReason.TimedOut)
            return WithProfile(UsageReadResult.Unknown("Claude-Usage-Abfrage hat das Zeitlimit überschritten."), profileId);
        if (read.Process.ExitCode != 0)
            return WithProfile(UsageReadResult.Unknown(
                $"Claude-Usage-Befehl wurde mit Exitcode {read.Process.ExitCode} beendet."), profileId);

        return ParseOutput(read.Output, profileId).Result;
    }

    /// <summary>Tests the configured parser without starting a process or requiring Claude login.</summary>
    public ClaudeUsageTestResult TestSample(string sampleOutput)
    {
        ArgumentNullException.ThrowIfNull(sampleOutput);
        (UsageReadResult Result, string? MatchedText)? parsed = directProfileId is { } profileId
            ? ParseOutput(sampleOutput, profileId)
            : null;
        var normalized = parsed is not null
            ? parsed.Value.Result
            : UsageReadResult.Unknown("Claude-Usage-Test benötigt eine Profil-ID.");
        return new ClaudeUsageTestResult(sampleOutput, parsed?.MatchedText, normalized);
    }

    private (UsageReadResult Result, string? MatchedText) ParseOutput(string output, PlatformProfileId profileId)
    {
        var readAtUtc = clock.UtcNow;
        var session = CommandRegexReader.Parse(output, CreateRequest());
        if (session.IsMatch && session.ResetAtUtc is null
            && options.Usage.Pattern != ClaudeUsageOptions.DefaultPattern)
        {
            var standard = CommandRegexReader.Parse(output,
                CreateRequest(pattern: ClaudeUsageOptions.DefaultPattern));
            if (standard.IsMatch && standard.Percent == session.Percent && standard.ResetAtUtc is not null)
                session = session with { ResetAtUtc = standard.ResetAtUtc };
        }
        var week = CommandRegexReader.Parse(output, CreateRequest(pattern: options.Usage.WeeklyPattern));
        try
        {
            var costMatch = Regex.Match(output, options.Usage.CostPattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, options.Usage.RegexTimeout);
            var cost = costMatch.Success && costMatch.Groups["cost"].Success
                ? costMatch.Groups["cost"].Value.Trim() : null;
            var freeMarker = Regex.IsMatch(output, options.Usage.FreeAccountPattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, options.Usage.RegexTimeout);
            var subscriptionMarker = output.Contains("using your subscription", StringComparison.OrdinalIgnoreCase);
            var freeAccount = !subscriptionMarker && freeMarker;
            var windows = new List<UsageWindow>();
            if (freeAccount)
            {
                windows.Add(new UsageWindow(options.Usage.WindowName, new UsagePercent(100), null,
                    options.Usage.Source, readAtUtc, UsageQuality.Aktuell));
            }
            else
            {
                if (session.IsMatch && session.Percent.HasValue)
                    windows.Add(new UsageWindow(options.Usage.WindowName, new UsagePercent(session.Percent.Value),
                        session.ResetAtUtc, options.Usage.Source, readAtUtc, UsageQuality.Aktuell));
                if (week.IsMatch && week.Percent.HasValue)
                    windows.Add(new UsageWindow("Current week (all models)", new UsagePercent(week.Percent.Value),
                        week.ResetAtUtc, options.Usage.Source, readAtUtc, UsageQuality.Aktuell,
                        windowDuration: TimeSpan.FromDays(7)));
            }

            if (!freeAccount && (session.Error is not null || week.Error is not null))
                return (UsageReadResult.Unknown(WithCost(session.Error ?? week.Error!, cost))
                    .ForProfile(profileId), session.MatchedText);
            if (windows.Count == 0)
                return (UsageReadResult.Unknown(WithCost(
                        "Claude-Usage-Ausgabe stimmt mit keinem Muster überein.", cost))
                    .ForProfile(profileId), session.MatchedText);

            var message = string.Join(" | ", new[]
            {
                freeAccount ? "Free account: usage assumed at 100%." : null,
                cost is null ? null : $"Total cost: {cost}"
            }.Where(value => value is not null));
            var snapshot = new UsageSnapshot(ClaudePlatform.Id, profileId, readAtUtc, options.Usage.Source,
                UsageQuality.Aktuell, windows);
            return (new UsageReadResult(UsageReadStatus.Available, snapshot, message, profileId),
                session.MatchedText ?? week.MatchedText);
        }
        catch (RegexMatchTimeoutException)
        {
            return (UsageReadResult.Unknown("Claude-Regex-Auswertung hat das Zeitlimit überschritten.")
                .ForProfile(profileId), session.MatchedText);
        }
        catch (ArgumentException exception)
        {
            return (UsageReadResult.Unknown($"Ungültiger Claude-Regex-Ausdruck: {exception.Message}")
                .ForProfile(profileId), session.MatchedText);
        }
    }

    private static string WithCost(string message, string? cost) =>
        cost is null ? message : $"{message} | Total cost: {cost}";

    public static UsageReadResult Normalize(CommandRegexParseResult parsed, DateTimeOffset readAtUtc,
        ClaudeUsageOptions options, PlatformProfileId profileId)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(options);
        if (!parsed.IsMatch || !parsed.Percent.HasValue)
            return UsageReadResult.Unknown(parsed.Error ?? "Claude-Usage-Ausgabe stimmt mit keinem Muster überein.")
                .ForProfile(profileId);

        try
        {
            var used = new UsagePercent(parsed.Percent.Value);
            var window = new UsageWindow(options.WindowName, used, parsed.ResetAtUtc,
                options.Source, readAtUtc, UsageQuality.Aktuell);
            return new UsageReadResult(UsageReadStatus.Available, new UsageSnapshot(ClaudePlatform.Id, profileId, readAtUtc,
                options.Source, UsageQuality.Aktuell, [window]), platformProfileId: profileId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return UsageReadResult.Unknown(exception.Message).ForProfile(profileId);
        }
    }

    private CommandRegexReadRequest CreateRequest(string? executable = null, PlatformProfile? profile = null,
        string? pattern = null)
    {
        ClaudeUsageOptions usage = options.Usage;
        return new CommandRegexReadRequest
        {
            Executable = executable ?? usage.Executable,
            Arguments = usage.Arguments,
            Pattern = pattern ?? usage.Pattern,
            UsedGroupName = usage.UsedGroupName,
            ResetGroupName = usage.ResetGroupName,
            ResetFormat = usage.ResetFormat,
            ConfiguredResetAtUtc = usage.ConfiguredResetAtUtc,
            ReferenceTimeUtc = clock.UtcNow,
            Culture = usage.Culture,
            Unit = usage.Unit,
            RegexTimeout = usage.RegexTimeout,
            CommandTimeout = usage.CommandTimeout,
            EnvironmentVariables = profile is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ClaudeProcessEnvironment.ForProfile(profile.ConfigurationDirectory),
            SuppressOutputLogging = true
        };
    }

    private static UsageReadResult WithProfile(UsageReadResult result, PlatformProfileId profileId) =>
        result.ForProfile(profileId);
}

public sealed record ClaudeUsageTestResult(string SampleOutput, string? MatchedText,
    UsageReadResult NormalizedResult);
