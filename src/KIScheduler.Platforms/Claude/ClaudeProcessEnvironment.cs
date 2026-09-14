namespace KIScheduler.Platforms.Claude;

internal static class ClaudeProcessEnvironment
{
    public static IReadOnlyDictionary<string, string> ForProfile(string configurationDirectory) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLAUDE_CONFIG_DIR"] = configurationDirectory
        };
}
