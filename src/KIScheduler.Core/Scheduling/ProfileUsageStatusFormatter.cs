using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public sealed record ProfileUsageStatus(PlatformId PlatformId, string ProfileDisplayName,
    UsageSnapshot? Usage, bool PlatformEnabled, bool ProfileEnabled, bool ShowUsageInStatusBar);

public static class ProfileUsageStatusFormatter
{
    public static string Format(IEnumerable<ProfileUsageStatus> profiles, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return string.Join("  |  ", profiles
            .Where(profile => profile.PlatformEnabled && profile.ProfileEnabled && profile.ShowUsageInStatusBar)
            .Select(profile =>
                $"{profile.PlatformId.Value}/{profile.ProfileDisplayName}: "
                + UsageStatusFormatter.Format(profile.Usage, nowUtc)));
    }
}
