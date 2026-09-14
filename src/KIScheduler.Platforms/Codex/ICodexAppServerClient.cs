using System.Text.Json;
using System.Text.Json.Serialization;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms.Codex;

public interface ICodexAppServerClient : IAsyncDisposable
{
    PlatformProfileId? PlatformProfileId => null;
    event EventHandler<CodexRateLimitsChangedEventArgs>? RateLimitsChanged;
    Task<CodexRateLimitsResponse> ReadRateLimitsAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexRateLimitsChangedEventArgs : EventArgs
{
    public CodexRateLimitsChangedEventArgs(CodexRateLimitsResponse rateLimits)
        : this(null, rateLimits) { }

    public CodexRateLimitsChangedEventArgs(PlatformProfileId? platformProfileId,
        CodexRateLimitsResponse rateLimits)
    {
        PlatformProfileId = platformProfileId;
        RateLimits = rateLimits ?? throw new ArgumentNullException(nameof(rateLimits));
    }

    public PlatformProfileId? PlatformProfileId { get; }
    public CodexRateLimitsResponse RateLimits { get; }
}

public enum CodexAppServerFailureKind
{
    Authentication,
    Connection,
    Parse,
    Protocol
}

public sealed class CodexAppServerException : Exception
{
    public CodexAppServerException(CodexAppServerFailureKind kind, string message,
        Exception? innerException = null) : base(message, innerException) => Kind = kind;

    public CodexAppServerFailureKind Kind { get; }
}

public sealed class CodexRateLimitsResponse
{
    [JsonPropertyName("rateLimits")]
    public CodexRateLimitBucket? RateLimits { get; init; }

    [JsonPropertyName("rateLimitsByLimitId")]
    public Dictionary<string, CodexRateLimitBucket>? RateLimitsByLimitId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class CodexRateLimitBucket
{
    [JsonPropertyName("limitId")]
    public string? LimitId { get; init; }

    [JsonPropertyName("limitName")]
    public string? LimitName { get; init; }

    [JsonPropertyName("primary")]
    public CodexRateLimitWindow? Primary { get; init; }

    [JsonPropertyName("secondary")]
    public CodexRateLimitWindow? Secondary { get; init; }

    [JsonPropertyName("rateLimitReachedType")]
    public string? RateLimitReachedType { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class CodexRateLimitWindow
{
    [JsonPropertyName("usedPercent")]
    public decimal? UsedPercent { get; init; }

    [JsonPropertyName("windowDurationMins")]
    public decimal? WindowDurationMins { get; init; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

internal static class CodexRateLimitsJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal static CodexRateLimitsResponse Parse(JsonElement value)
    {
        try
        {
            return value.Deserialize<CodexRateLimitsResponse>(Options)
                ?? throw new JsonException("Leere Rate-Limit-Antwort.");
        }
        catch (JsonException exception)
        {
            throw new CodexAppServerException(CodexAppServerFailureKind.Parse,
                "Die Rate-Limit-Antwort des Codex App Servers ist ungültig.", exception);
        }
    }
}
