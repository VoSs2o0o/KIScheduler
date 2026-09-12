using System.Text.RegularExpressions;

namespace KIScheduler.Core.Security;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        value = AuthorizationHeader().Replace(value, "$1<redacted>");
        value = JsonSecret().Replace(value, "$1<redacted>$3");
        return AssignedSecret().Replace(value, "$1<redacted>");
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*)(?:bearer\\s+)?[^\\s,;]+", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex("(?i)([\\\"'](?:access[_-]?token|api[_-]?token|api[_-]?key|token|secret|password|credential)[\\\"']\\s*:\\s*[\\\"'])([^\\\"']*)([\\\"'])",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex JsonSecret();

    [GeneratedRegex("(?i)((?:access[_-]?token|api[_-]?token|api[_-]?key|token|secret|password|credential)\\s*=\\s*)[^\\s,;]+",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex AssignedSecret();
}
