using System.Globalization;
using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public static class UsageStatusFormatter
{
    public static string Format(UsageSnapshot? snapshot, DateTimeOffset nowUtc)
    {
        if (snapshot is null || snapshot.Windows.Count == 0) return "unbekannt";

        var percentages = string.Join("/", snapshot.Windows.Select(window =>
            $"{window.UsedPercent.Value.ToString("0.##", CultureInfo.CurrentCulture)}%"));
        var nextReset = snapshot.Windows
            .Where(window => window.ResetAtUtc > nowUtc)
            .Select(window => window.ResetAtUtc!.Value)
            .OrderBy(value => value)
            .FirstOrDefault();
        if (nextReset == default) return percentages;

        var remaining = nextReset - nowUtc;
        var totalMinutes = Math.Max(0, (long)Math.Floor(remaining.TotalMinutes));
        return $"{percentages}, {totalMinutes / 60}:{totalMinutes % 60:00}";
    }
}
