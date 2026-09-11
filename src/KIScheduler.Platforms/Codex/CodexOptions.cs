namespace KIScheduler.Platforms.Codex;

public sealed class CodexOptions
{
    public const string SectionName = "Codex";

    public string Executable { get; set; } = "codex";
    public string Sandbox { get; set; } = "workspace-write";
    public TimeSpan AvailabilityTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan AppServerRequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan UsageCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
    public List<string> AppServerArguments { get; set; } = ["app-server", "--listen", "stdio://"];

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(Sandbox);
        if (AvailabilityTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Codex:AvailabilityTimeout muss größer als null sein.");
        if (AppServerRequestTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Codex:AppServerRequestTimeout muss größer als null sein.");
        if (UsageCacheDuration < TimeSpan.Zero)
            throw new InvalidOperationException("Codex:UsageCacheDuration darf nicht negativ sein.");
        if (AppServerArguments.Count == 0 || AppServerArguments.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Codex:AppServerArguments muss gültige Argumente enthalten.");
    }
}
