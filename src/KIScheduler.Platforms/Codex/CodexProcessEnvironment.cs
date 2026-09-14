namespace KIScheduler.Platforms.Codex;

internal static class CodexProcessEnvironment
{
    public static IReadOnlyDictionary<string, string> ForProfile(string executable, string configurationDirectory)
    {
        // A desktop-launched scheduler can inherit a service/sandbox profile although the selected
        // Codex installation and its authenticated .codex directory belong to the interactive user.
        // Prefer the profile encoded in the absolute per-user installation path in that case.
        var profile = InferFromExecutable(executable);
        if (string.IsNullOrWhiteSpace(profile))
            profile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrWhiteSpace(profile))
            profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODEX_HOME"] = configurationDirectory,
            ["CODEX_SQLITE_HOME"] = configurationDirectory
        };
        if (!string.IsNullOrWhiteSpace(profile)) environment["USERPROFILE"] = profile;
        return environment;
    }

    private static string? InferFromExecutable(string executable)
    {
        if (!Path.IsPathFullyQualified(executable)) return null;
        DirectoryInfo? directory = new FileInfo(executable).Directory;
        while (directory is not null)
        {
            if (directory.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase))
                return directory.Parent?.FullName;
            directory = directory.Parent;
        }
        return null;
    }
}
