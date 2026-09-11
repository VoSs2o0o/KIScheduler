using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Claude;

public sealed class ClaudeUsageProvider : IUsageProvider
{
    private readonly CommandRegexReader reader;
    private readonly IClock clock;
    private readonly ClaudeOptions options;

    public ClaudeUsageProvider(CommandRegexReader reader, IClock clock, IOptions<ClaudeOptions> options)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public PlatformId PlatformId => ClaudePlatform.Id;
    public UsageProviderCapabilities Capabilities => new(false, false,
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
        CommandRegexReadResult read = await reader.ReadAsync(CreateRequest(), cancellationToken)
            .ConfigureAwait(false);

        if (read.Process.TerminationReason == ProcessTerminationReason.StartFailed)
            return new UsageReadResult(UsageReadStatus.ProviderUnavailable, message:
                $"Claude-Usage-Befehl konnte nicht gestartet werden: {read.Process.StartError}");
        if (read.Process.TerminationReason == ProcessTerminationReason.Cancelled)
            return UsageReadResult.Unknown("Claude-Usage-Abfrage wurde abgebrochen.");
        if (read.Process.TerminationReason == ProcessTerminationReason.TimedOut)
            return UsageReadResult.Unknown("Claude-Usage-Abfrage hat das Zeitlimit überschritten.");
        if (read.Process.ExitCode != 0)
            return UsageReadResult.Unknown(
                $"Claude-Usage-Befehl wurde mit Exitcode {read.Process.ExitCode} beendet.");

        return Normalize(read.Parsed, clock.UtcNow, options.Usage);
    }

    /// <summary>Tests the configured parser without starting a process or requiring Claude login.</summary>
    public ClaudeUsageTestResult TestSample(string sampleOutput)
    {
        ArgumentNullException.ThrowIfNull(sampleOutput);
        CommandRegexParseResult parsed = CommandRegexReader.Parse(sampleOutput, CreateRequest());
        return new ClaudeUsageTestResult(sampleOutput, parsed.MatchedText,
            Normalize(parsed, clock.UtcNow, options.Usage));
    }

    public static UsageReadResult Normalize(CommandRegexParseResult parsed, DateTimeOffset readAtUtc,
        ClaudeUsageOptions options)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(options);
        if (!parsed.IsMatch || !parsed.Percent.HasValue)
            return UsageReadResult.Unknown(parsed.Error ?? "Claude-Usage-Ausgabe stimmt mit keinem Muster überein.");

        try
        {
            var used = new UsagePercent(parsed.Percent.Value);
            var window = new UsageWindow(options.WindowName, used, parsed.ResetAtUtc,
                options.Source, readAtUtc, UsageQuality.Aktuell);
            return UsageReadResult.Available(new UsageSnapshot(ClaudePlatform.Id, readAtUtc,
                options.Source, UsageQuality.Aktuell, [window]));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return UsageReadResult.Unknown(exception.Message);
        }
    }

    private CommandRegexReadRequest CreateRequest()
    {
        ClaudeUsageOptions usage = options.Usage;
        return new CommandRegexReadRequest
        {
            Executable = usage.Executable,
            Arguments = usage.Arguments,
            Pattern = usage.Pattern,
            UsedGroupName = usage.UsedGroupName,
            ResetGroupName = usage.ResetGroupName,
            ResetFormat = usage.ResetFormat,
            ConfiguredResetAtUtc = usage.ConfiguredResetAtUtc,
            Culture = usage.Culture,
            Unit = usage.Unit,
            RegexTimeout = usage.RegexTimeout,
            CommandTimeout = usage.CommandTimeout,
            SuppressOutputLogging = true
        };
    }
}

public sealed record ClaudeUsageTestResult(string SampleOutput, string? MatchedText,
    UsageReadResult NormalizedResult);
