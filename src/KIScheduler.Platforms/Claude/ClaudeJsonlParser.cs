using System.Text.Json;
using KIScheduler.Core.Contracts;

namespace KIScheduler.Platforms.Claude;

public sealed record ClaudeJsonlParseResult(IReadOnlyList<PlatformExecutionEvent> Events,
    string? SessionId, string? FinalMessage, string? ErrorMessage, bool UsageExceeded,
    bool AuthenticationError, bool NetworkError, string? ParseError);

public static class ClaudeJsonlParser
{
    private static readonly string[] QuotaMarkers =
    [
        "rate_limit_error", "rate limit reached", "usage limit reached", "usage_limit",
        "quota exceeded", "you've hit your limit", "you have hit your limit"
    ];
    private static readonly string[] AuthenticationMarkers =
        ["authentication_error", "unauthorized", "not logged in", "login required", "invalid api key", "401"];
    private static readonly string[] NetworkMarkers =
        ["connection refused", "connection reset", "network error", "dns", "unreachable", "503"];

    public static ClaudeJsonlParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var events = new List<PlatformExecutionEvent>();
        string? sessionId = null;
        string? finalMessage = null;
        string? errorMessage = null;
        string? parseError = null;
        bool usageExceeded = false;
        bool authenticationError = false;
        bool networkError = false;

        foreach (string line in lines.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    parseError ??= "Claude-JSONL enthält ein Ereignis, das kein JSON-Objekt ist.";
                    continue;
                }

                string type = ReadString(root, "type") ?? "unknown";
                events.Add(new PlatformExecutionEvent(type, root.GetRawText()));
                sessionId = ReadString(root, "session_id") ?? sessionId;

                if (type.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    finalMessage = ReadString(root, "result") ?? finalMessage;
                    if (ReadBoolean(root, "is_error"))
                        errorMessage = finalMessage ?? FindFirstString(root, "message") ?? errorMessage;
                }
                else if (type.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = FindFirstString(root, "message") ?? errorMessage;
                }

                string searchable = root.GetRawText();
                usageExceeded |= ContainsAny(searchable, QuotaMarkers);
                authenticationError |= ContainsAny(searchable, AuthenticationMarkers);
                networkError |= ContainsAny(searchable, NetworkMarkers);
            }
            catch (JsonException exception)
            {
                parseError ??= $"Claude-JSONL konnte nicht gelesen werden: {exception.Message}";
            }
        }

        return new ClaudeJsonlParseResult(events, sessionId, finalMessage, errorMessage, usageExceeded,
            authenticationError, networkError, parseError);
    }

    public static bool ContainsUsageExceededText(string? value) => ContainsAny(value, QuotaMarkers);
    public static bool ContainsAuthenticationText(string? value) => ContainsAny(value, AuthenticationMarkers);
    public static bool ContainsNetworkText(string? value) => ContainsAny(value, NetworkMarkers);

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value)
        && value.ValueKind is JsonValueKind.True;

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FindFirstString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                string? nested = FindFirstString(property.Value, propertyName);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? nested = FindFirstString(item, propertyName);
                if (nested is not null) return nested;
            }
        }
        return null;
    }

    private static bool ContainsAny(string? value, IEnumerable<string> markers) =>
        !string.IsNullOrWhiteSpace(value) && markers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
