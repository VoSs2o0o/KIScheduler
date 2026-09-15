using System.Globalization;
using System.Text.RegularExpressions;
using KIScheduler.Core.Contracts;

namespace KIScheduler.Platforms.Claude;

/// <summary>
/// Reusable command/text reader. It deliberately has no platform or usage-domain knowledge.
/// </summary>
public sealed class CommandRegexReader(IProcessRunner processRunner)
{
    private readonly IProcessRunner processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<CommandRegexReadResult> ReadAsync(CommandRegexReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProcessRunResult process = await processRunner.RunAsync(new ProcessRunRequest(request.Executable)
        {
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
            StandardInput = request.StandardInput,
            EnvironmentVariables = request.EnvironmentVariables,
            SensitiveEnvironmentVariableNames = request.SensitiveEnvironmentVariableNames,
            Timeout = request.CommandTimeout,
            SuppressOutputLogging = request.SuppressOutputLogging
        }, cancellationToken).ConfigureAwait(false);

        string text = string.Join(Environment.NewLine, process.Output
            .OrderBy(line => line.Sequence)
            .Select(line => line.Text));
        CommandRegexParseResult parsed = Parse(text, request);
        return new CommandRegexReadResult(process, text, parsed);
    }

    public static CommandRegexParseResult Parse(string text, CommandRegexReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(request);
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(request.Culture);
        }
        catch (CultureNotFoundException exception)
        {
            return CommandRegexParseResult.Invalid($"Unbekannte Kultur '{request.Culture}': {exception.Message}");
        }

        try
        {
            var regex = new Regex(request.Pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                request.RegexTimeout);
            Match match = regex.Match(text);
            if (!match.Success)
                return CommandRegexParseResult.NoMatch();

            Group usedGroup = match.Groups[request.UsedGroupName];
            if (!usedGroup.Success)
                return CommandRegexParseResult.Invalid(
                    $"Der benannte Regex-Wert '{request.UsedGroupName}' wurde nicht gefunden.", match.Value);
            if (!decimal.TryParse(usedGroup.Value, NumberStyles.Number, culture, out decimal value))
                return CommandRegexParseResult.Invalid(
                    $"'{usedGroup.Value}' ist mit Kultur '{request.Culture}' keine Zahl.", match.Value);

            decimal percent = request.Unit switch
            {
                RegexValueUnit.Percent => value,
                RegexValueUnit.Fraction => value * 100m,
                _ => throw new ArgumentOutOfRangeException(nameof(request), "Unbekannte Regex-Einheit.")
            };
            if (percent is < 0m or > 100m)
                return CommandRegexParseResult.Invalid(
                    $"Der normalisierte Prozentwert {percent} liegt nicht zwischen 0 und 100.", match.Value);

            DateTimeOffset? resetAtUtc = request.ConfiguredResetAtUtc;
            if (!string.IsNullOrWhiteSpace(request.ResetGroupName)
                && match.Groups[request.ResetGroupName].Success)
            {
                string resetText = match.Groups[request.ResetGroupName].Value;
                bool resetParsed = TryParseReset(resetText, request, culture, out DateTimeOffset reset);
                if (!resetParsed)
                    return CommandRegexParseResult.Invalid(
                        $"Resetzeit '{resetText}' konnte nicht gelesen werden.", match.Value);
                resetAtUtc = reset;
            }

            return CommandRegexParseResult.Matched(percent, resetAtUtc, match.Value);
        }
        catch (RegexMatchTimeoutException)
        {
            return CommandRegexParseResult.Invalid(
                $"Regex-Auswertung hat das Zeitlimit von {request.RegexTimeout} überschritten.");
        }
        catch (ArgumentException exception)
        {
            return CommandRegexParseResult.Invalid($"Ungültiger regulärer Ausdruck: {exception.Message}");
        }
    }

    private static bool TryParseReset(string text, CommandRegexReadRequest request, CultureInfo culture,
        out DateTimeOffset reset)
    {
        if (!string.IsNullOrWhiteSpace(request.ResetFormat))
            return DateTimeOffset.TryParseExact(text, request.ResetFormat, culture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out reset);
        if (DateTimeOffset.TryParse(text, culture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out reset)) return true;

        // Claude prints a yearless local date and an IANA time-zone name.
        var zoneStart = text.LastIndexOf(" (", StringComparison.Ordinal);
        if (zoneStart < 0 || !text.EndsWith(')')) return false;
        var zoneId = text[(zoneStart + 2)..^1];
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }

        var reference = request.ReferenceTimeUtc ?? DateTimeOffset.UtcNow;
        var localReference = TimeZoneInfo.ConvertTime(reference, zone);
        var dateText = text[..zoneStart].Trim();
        DateTime? closest = null;
        for (var year = localReference.Year - 1; year <= localReference.Year + 1; year++)
        {
            if (!DateTime.TryParseExact($"{dateText} {year}", "MMM d, h:mmtt yyyy",
                    CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var candidate)) continue;
            if (closest is null || Math.Abs((candidate - localReference.DateTime).Ticks)
                < Math.Abs((closest.Value - localReference.DateTime).Ticks)) closest = candidate;
        }
        if (closest is null || zone.IsInvalidTime(closest.Value) || zone.IsAmbiguousTime(closest.Value)) return false;
        reset = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(closest.Value, zone));
        return true;
    }
}

public sealed record CommandRegexReadRequest
{
    public required string Executable { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public string? StandardInput { get; init; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> SensitiveEnvironmentVariableNames { get; init; } = [];
    public required string Pattern { get; init; }
    public string UsedGroupName { get; init; } = "used";
    public string? ResetGroupName { get; init; } = "reset";
    public string? ResetFormat { get; init; }
    public DateTimeOffset? ConfiguredResetAtUtc { get; init; }
    public DateTimeOffset? ReferenceTimeUtc { get; init; }
    public string Culture { get; init; } = "en-US";
    public RegexValueUnit Unit { get; init; } = RegexValueUnit.Percent;
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public bool SuppressOutputLogging { get; init; }
}

public sealed record CommandRegexParseResult(bool IsMatch, decimal? Percent, DateTimeOffset? ResetAtUtc,
    string? MatchedText, string? Error)
{
    internal static CommandRegexParseResult Matched(decimal percent, DateTimeOffset? resetAtUtc, string matchedText) =>
        new(true, percent, resetAtUtc, matchedText, null);
    internal static CommandRegexParseResult NoMatch() => new(false, null, null, null, null);
    internal static CommandRegexParseResult Invalid(string error, string? matchedText = null) =>
        new(false, null, null, matchedText, error);
}

public sealed record CommandRegexReadResult(ProcessRunResult Process, string Output,
    CommandRegexParseResult Parsed);
