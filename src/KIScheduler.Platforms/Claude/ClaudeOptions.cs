namespace KIScheduler.Platforms.Claude;

public sealed class ClaudeOptions
{
    public const string SectionName = "Claude";

    public string Executable { get; set; } = "claude";
    public string PermissionMode { get; set; } = "acceptEdits";
    public TimeSpan AvailabilityTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public List<string> AdditionalArguments { get; set; } = [];
    public ClaudeUsageOptions Usage { get; set; } = new();

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(PermissionMode);
        if (AvailabilityTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Claude:AvailabilityTimeout muss größer als null sein.");
        if (AdditionalArguments.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Claude:AdditionalArguments darf keine leeren Argumente enthalten.");
        Usage.Validate();
    }
}

public sealed class ClaudeUsageOptions
{
    public const string DefaultPattern = @"Current session:\s*(?<used>\d+(?:\.\d+)?)%\s*used(?:\s*[·•]\s*resets\s+(?<reset>[A-Za-z]{3}\s+\d{1,2},\s*\d{1,2}:\d{2}(?:am|pm)\s*\([^)]+\)))?";
    public const string DefaultWeeklyPattern = @"Current week\s*\(all models\):\s*(?<used>\d+(?:\.\d+)?)%\s*used(?:\s*[·•]\s*resets\s+(?<reset>[A-Za-z]{3}\s+\d{1,2},\s*\d{1,2}:\d{2}(?:am|pm)\s*\([^)]+\)))?";
    public const string DefaultCostPattern = @"Total cost:\s*(?<cost>\$\s*\d+(?:\.\d+)?)";
    public const string DefaultFreeAccountPattern = @"\bfree\s+(?:account|plan|tier)\b";

    public string Executable { get; set; } = "claude";
    public List<string> Arguments { get; set; } = ["-p", "/usage"];
    public string Pattern { get; set; } = DefaultPattern;
    public string WeeklyPattern { get; set; } = DefaultWeeklyPattern;
    public string CostPattern { get; set; } = DefaultCostPattern;
    public string FreeAccountPattern { get; set; } = DefaultFreeAccountPattern;
    public string UsedGroupName { get; set; } = "used";
    public string? ResetGroupName { get; set; } = "reset";
    public string? ResetFormat { get; set; }
    public DateTimeOffset? ConfiguredResetAtUtc { get; set; }
    public string Culture { get; set; } = "en-US";
    public RegexValueUnit Unit { get; set; } = RegexValueUnit.Percent;
    public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public string WindowName { get; set; } = "Current session";
    public string Source { get; set; } = "claude-command-regex";

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(WeeklyPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(CostPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(FreeAccountPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(UsedGroupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(WindowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);
        if (Arguments.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Claude:Usage:Arguments darf keine leeren Argumente enthalten.");
        if (RegexTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Claude:Usage:RegexTimeout muss größer als null sein.");
        if (CommandTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Claude:Usage:CommandTimeout muss größer als null sein.");
        if (ConfiguredResetAtUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new InvalidOperationException("Claude:Usage:ConfiguredResetAtUtc muss in UTC angegeben werden.");
    }
}

public enum RegexValueUnit
{
    Percent,
    Fraction
}
