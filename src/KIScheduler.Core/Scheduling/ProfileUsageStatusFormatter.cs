using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public sealed record ProfileUsageStatus(PlatformId PlatformId, string ProfileDisplayName,
    UsageSnapshot? Usage, bool PlatformEnabled, bool ProfileEnabled, bool ShowUsageInStatusBar,
    string? UsageMessage = null);

public static class ProfileUsageStatusFormatter
{
    public static string Format(IEnumerable<ProfileUsageStatus> profiles, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return string.Join("  |  ", profiles
            .Where(profile => profile.PlatformEnabled && profile.ProfileEnabled && profile.ShowUsageInStatusBar)
            .Select(profile =>
                $"{profile.PlatformId.Value}/{profile.ProfileDisplayName}: "
                + (profile.PlatformId.Value.Equals("claude", StringComparison.OrdinalIgnoreCase)
                    ? FormatClaudeCost(profile.UsageMessage)
                    : UsageStatusFormatter.Format(profile.Usage, nowUtc))));
    }

    private static string FormatClaudeCost(string? message)
    {
        const string label = "Total cost:";
        var index = message?.IndexOf(label, StringComparison.OrdinalIgnoreCase) ?? -1;
        if (index < 0) return "Total cost: unbekannt";
        var value = message![(index + label.Length)..];
        var separator = value.IndexOf('|');
        if (separator >= 0) value = value[..separator];
        value = value.Trim();
        return value.Length == 0 ? "Total cost: unbekannt" : $"Total cost: {value}";
    }
}
