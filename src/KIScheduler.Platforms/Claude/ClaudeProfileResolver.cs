using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms.Claude;

public enum ClaudeProfileFailureKind
{
    NotFound,
    Disabled,
    WrongPlatform,
    DirectoryMissing,
    DirectoryNotReadable
}

public sealed class ClaudeProfileException : Exception
{
    public ClaudeProfileException(ClaudeProfileFailureKind kind, string message, Exception? innerException = null)
        : base(message) => Kind = kind;

    public ClaudeProfileFailureKind Kind { get; }
}

public interface IClaudeProfileResolver
{
    Task<PlatformProfile> ResolveAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default);
    Task<PlatformProfile> ResolveDefaultAsync(CancellationToken cancellationToken = default);
}

public sealed class ClaudeProfileResolver(IPlatformProfileRepository profiles) : IClaudeProfileResolver
{
    public async Task<PlatformProfile> ResolveAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        PlatformProfile profile = await profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new ClaudeProfileException(ClaudeProfileFailureKind.NotFound,
                $"Das Claude-Profil '{profileId}' wurde nicht gefunden.");
        return Validate(profile);
    }

    public async Task<PlatformProfile> ResolveDefaultAsync(CancellationToken cancellationToken = default)
    {
        PlatformProfile profile = await profiles.GetDefaultAsync(ClaudePlatform.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ClaudeProfileException(ClaudeProfileFailureKind.NotFound,
                "Für Claude ist kein Standardprofil konfiguriert.");
        return Validate(profile);
    }

    private static PlatformProfile Validate(PlatformProfile profile)
    {
        if (!profile.PlatformId.Value.Equals(ClaudePlatform.Id.Value, StringComparison.OrdinalIgnoreCase))
            throw new ClaudeProfileException(ClaudeProfileFailureKind.WrongPlatform,
                $"Das Profil '{profile.DisplayName}' gehört nicht zur Claude-Plattform.");
        if (!profile.Enabled)
            throw new ClaudeProfileException(ClaudeProfileFailureKind.Disabled,
                $"Das Claude-Profil '{profile.DisplayName}' ist deaktiviert.");
        if (!Directory.Exists(profile.ConfigurationDirectory))
            throw new ClaudeProfileException(ClaudeProfileFailureKind.DirectoryMissing,
                $"Der Konfigurationsordner des Claude-Profils '{profile.DisplayName}' fehlt: " +
                profile.ConfigurationDirectory);

        try
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(profile.ConfigurationDirectory)
                .GetEnumerator();
            _ = entries.MoveNext();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new ClaudeProfileException(ClaudeProfileFailureKind.DirectoryNotReadable,
                $"Der Konfigurationsordner des Claude-Profils '{profile.DisplayName}' ist nicht lesbar.", exception);
        }

        return profile;
    }
}
