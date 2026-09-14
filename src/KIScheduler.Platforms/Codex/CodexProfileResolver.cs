using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms.Codex;

public enum CodexProfileFailureKind
{
    NotFound,
    Disabled,
    WrongPlatform,
    DirectoryMissing,
    DirectoryNotReadable
}

public sealed class CodexProfileException : Exception
{
    public CodexProfileException(CodexProfileFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException) => Kind = kind;

    public CodexProfileFailureKind Kind { get; }
}

public interface ICodexProfileResolver
{
    Task<PlatformProfile> ResolveAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default);
    Task<PlatformProfile> ResolveDefaultAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexProfileResolver(IPlatformProfileRepository profiles) : ICodexProfileResolver
{
    public async Task<PlatformProfile> ResolveAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        PlatformProfile profile = await profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new CodexProfileException(CodexProfileFailureKind.NotFound,
                $"Das Codex-Profil '{profileId}' wurde nicht gefunden.");
        return Validate(profile);
    }

    public async Task<PlatformProfile> ResolveDefaultAsync(CancellationToken cancellationToken = default)
    {
        PlatformProfile profile = await profiles.GetDefaultAsync(CodexPlatform.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CodexProfileException(CodexProfileFailureKind.NotFound,
                "Für Codex ist kein Standardprofil konfiguriert.");
        return Validate(profile);
    }

    private static PlatformProfile Validate(PlatformProfile profile)
    {
        if (!profile.PlatformId.Value.Equals(CodexPlatform.Id.Value, StringComparison.OrdinalIgnoreCase))
            throw new CodexProfileException(CodexProfileFailureKind.WrongPlatform,
                $"Das Profil '{profile.DisplayName}' gehört nicht zur Codex-Plattform.");
        if (!profile.Enabled)
            throw new CodexProfileException(CodexProfileFailureKind.Disabled,
                $"Das Codex-Profil '{profile.DisplayName}' ist deaktiviert.");
        if (!Directory.Exists(profile.ConfigurationDirectory))
            throw new CodexProfileException(CodexProfileFailureKind.DirectoryMissing,
                $"Der Konfigurationsordner des Codex-Profils '{profile.DisplayName}' fehlt: " +
                profile.ConfigurationDirectory);

        try
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(profile.ConfigurationDirectory)
                .GetEnumerator();
            _ = entries.MoveNext();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new CodexProfileException(CodexProfileFailureKind.DirectoryNotReadable,
                $"Der Konfigurationsordner des Codex-Profils '{profile.DisplayName}' ist nicht lesbar.", exception);
        }

        return profile;
    }
}
