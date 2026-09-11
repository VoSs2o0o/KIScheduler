using System.Text.Json;
using KIScheduler.Core.Contracts;

namespace KIScheduler.Platforms.Codex;

public sealed class CodexJsonlParseResult
{
    internal CodexJsonlParseResult(IReadOnlyList<PlatformExecutionEvent> events, string? sessionId,
        string? finalMessage, string? errorMessage, bool usageExceeded, bool authenticationError,
        bool networkError, string? parseError)
    {
        Events = events;
        SessionId = sessionId;
        FinalMessage = finalMessage;
        ErrorMessage = errorMessage;
        UsageExceeded = usageExceeded;
        AuthenticationError = authenticationError;
        NetworkError = networkError;
        ParseError = parseError;
    }

    public IReadOnlyList<PlatformExecutionEvent> Events { get; }
    public string? SessionId { get; }
    public string? FinalMessage { get; }
    public string? ErrorMessage { get; }
    public bool UsageExceeded { get; }
    public bool AuthenticationError { get; }
    public bool NetworkError { get; }
    public string? ParseError { get; }
}

public static class CodexJsonlParser
{
    private static readonly string[] AuthenticationMarkers =
        ["unauthorized", "authentication", "not logged in", "login required", "invalid api key", "401"];
    private static readonly string[] NetworkMarkers =
        ["connection refused", "connection reset", "network error", "dns", "timed out", "unreachable"];

    public static CodexJsonlParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var events = new List<PlatformExecutionEvent>();
        string? sessionId = null;
        string? finalMessage = null;
        string? errorMessage = null;
        string? parseError = null;
        bool structuredQuota = false;

        foreach (string line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    parseError ??= "Codex-JSONL enthält ein Ereignis, das kein JSON-Objekt ist.";
                    continue;
                }

                string type = ReadString(root, "type") ?? "unknown";
                events.Add(new PlatformExecutionEvent(type, root.GetRawText()));

                if (type.Equals("thread.started", StringComparison.OrdinalIgnoreCase))
                    sessionId ??= ReadString(root, "thread_id");

                if (type.Equals("item.completed", StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("item", out JsonElement item)
                    && string.Equals(ReadString(item, "type"), "agent_message", StringComparison.OrdinalIgnoreCase))
                    finalMessage = ReadString(item, "text") ?? finalMessage;

                if (type.Equals("error", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("turn.failed", StringComparison.OrdinalIgnoreCase))
                    errorMessage = FindFirstString(root, "message") ?? errorMessage;

                structuredQuota |= ContainsQuotaSignal(root);
            }
            catch (JsonException exception)
            {
                parseError ??= $"Codex-JSONL konnte nicht gelesen werden: {exception.Message}";
            }
        }

        string searchable = string.Join('\n', new[] { errorMessage, parseError }.Where(value => value is not null));
        return new CodexJsonlParseResult(events, sessionId, finalMessage, errorMessage,
            structuredQuota || ContainsAny(searchable, QuotaMarkers),
            ContainsAny(searchable, AuthenticationMarkers), ContainsAny(searchable, NetworkMarkers), parseError);
    }

    public static bool ContainsUsageExceededText(string? value) => ContainsAny(value, QuotaMarkers);
    public static bool ContainsAuthenticationText(string? value) => ContainsAny(value, AuthenticationMarkers);
    public static bool ContainsNetworkText(string? value) => ContainsAny(value, NetworkMarkers);

    private static string[] QuotaMarkers => QuotaMarkersHolder.Value;

    private static class QuotaMarkersHolder
    {
        internal static readonly string[] Value =
            ["rate_limit", "rate limit", "usage_limit", "usage limit", "quota exceeded", "usage exceeded"];
    }

    private static bool ContainsQuotaSignal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Equals("rateLimitReachedType", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    return true;
                if ((property.Name.Equals("type", StringComparison.OrdinalIgnoreCase)
                     || property.Name.Equals("code", StringComparison.OrdinalIgnoreCase))
                    && property.Value.ValueKind == JsonValueKind.String
                    && ContainsAny(property.Value.GetString(), QuotaMarkers))
                    return true;
                if (ContainsQuotaSignal(property.Value)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                if (ContainsQuotaSignal(item)) return true;
        }

        return false;
    }

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

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ContainsAny(string? value, IEnumerable<string> markers) =>
        !string.IsNullOrWhiteSpace(value) && markers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
