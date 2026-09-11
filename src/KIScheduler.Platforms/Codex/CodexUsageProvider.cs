using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KIScheduler.Platforms.Codex;

public sealed class CodexUsageProvider : IUsageProvider, IDisposable
{
    private const string Source = "codex-app-server/account/rateLimits/read";
    private readonly ICodexAppServerClient client;
    private readonly IClock clock;
    private readonly CodexOptions options;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private UsageReadResult current = UsageReadResult.Unknown("Codex-Usage wurde noch nicht gelesen.");
    private bool disposed;

    public CodexUsageProvider(ICodexAppServerClient client, IClock clock, IOptions<CodexOptions> options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        client.RateLimitsChanged += OnRateLimitsChanged;
    }

    public PlatformId PlatformId => CodexPlatform.Id;
    public UsageProviderCapabilities Capabilities { get; } = new(true, true, true);
    public event EventHandler<UsageChangedEventArgs>? UsageChanged;

    public async Task<UsageReadResult> ReadAsync(bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        UsageReadResult cached = Volatile.Read(ref current);
        if (!forceRefresh && IsFresh(cached)) return cached;

        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = Volatile.Read(ref current);
            if (!forceRefresh && IsFresh(cached)) return cached;
            try
            {
                CodexRateLimitsResponse response = await client.ReadRateLimitsAsync(cancellationToken)
                    .ConfigureAwait(false);
                UsageReadResult normalized = Normalize(response, clock.UtcNow);
                Volatile.Write(ref current, normalized);
                return normalized;
            }
            catch (CodexAppServerException exception)
            {
                UsageReadResult unknown = UsageReadResult.Unknown(
                    $"Codex-Usage unbekannt ({ToReason(exception.Kind)}): {exception.Message}");
                Volatile.Write(ref current, unknown);
                return unknown;
            }
            catch (Exception exception) when (exception is JsonException or FormatException
                or OverflowException or ArgumentOutOfRangeException)
            {
                UsageReadResult unknown = UsageReadResult.Unknown(
                    $"Codex-Usage unbekannt (parse): {exception.Message}");
                Volatile.Write(ref current, unknown);
                return unknown;
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public static UsageReadResult Normalize(CodexRateLimitsResponse response, DateTimeOffset readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        readAtUtc = readAtUtc.ToUniversalTime();
        var windows = new List<UsageWindow>();
        var missing = new List<string>();

        IEnumerable<KeyValuePair<string, CodexRateLimitBucket>> buckets = response.RateLimitsByLimitId is not null
            ? response.RateLimitsByLimitId
            : response.RateLimits is null
                ? []
                : [new KeyValuePair<string, CodexRateLimitBucket>(response.RateLimits.LimitId ?? "codex",
                    response.RateLimits)];

        foreach ((string dictionaryId, CodexRateLimitBucket bucket) in buckets)
        {
            string limitId = string.IsNullOrWhiteSpace(bucket.LimitId) ? dictionaryId : bucket.LimitId.Trim();
            AddWindow(bucket.Primary, "primary", limitId, bucket);
            AddWindow(bucket.Secondary, "secondary", limitId, bucket);
        }

        if (windows.Count == 0)
            return UsageReadResult.Unknown(missing.Count == 0
                ? "Codex-Rate-Limit-Antwort enthält keine Usage-Fenster."
                : $"Codex-Rate-Limit-Antwort enthält keine nutzbaren Fenster; fehlt: {string.Join(", ", missing)}.");

        var snapshot = new UsageSnapshot(CodexPlatform.Id, readAtUtc, Source, UsageQuality.Aktuell, windows);
        return new UsageReadResult(UsageReadStatus.Available, snapshot,
            missing.Count == 0 ? null : $"Fehlende Codex-Usage-Felder: {string.Join(", ", missing)}.");

        void AddWindow(CodexRateLimitWindow? value, string kind, string limitId, CodexRateLimitBucket bucket)
        {
            if (value is null) return;
            string identity = $"{limitId}:{kind}";
            if (!value.UsedPercent.HasValue)
            {
                missing.Add($"{identity}.usedPercent");
                return;
            }
            if (value.UsedPercent.Value is < 0 or > 100)
                throw new FormatException($"{identity}.usedPercent liegt außerhalb von 0 bis 100.");

            DateTimeOffset? resetAt = null;
            if (value.ResetsAt.HasValue)
                resetAt = DateTimeOffset.FromUnixTimeSeconds(value.ResetsAt.Value);
            else
                missing.Add($"{identity}.resetsAt");

            TimeSpan? duration = null;
            if (value.WindowDurationMins is > 0)
                duration = TimeSpan.FromMinutes(decimal.ToDouble(value.WindowDurationMins.Value));
            else
                missing.Add($"{identity}.windowDurationMins");

            windows.Add(new UsageWindow(identity, new UsagePercent(value.UsedPercent.Value), resetAt,
                Source, readAtUtc, UsageQuality.Aktuell, bucket.RateLimitReachedType, duration,
                limitId, bucket.LimitName));
        }
    }

    private bool IsFresh(UsageReadResult result) => result.Snapshot is { } snapshot
        && clock.UtcNow - snapshot.ReadAtUtc <= options.UsageCacheDuration;

    private void OnRateLimitsChanged(object? sender, CodexRateLimitsChangedEventArgs args)
    {
        UsageReadResult result;
        try
        {
            result = Normalize(args.RateLimits, clock.UtcNow);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException
            or ArgumentOutOfRangeException)
        {
            result = UsageReadResult.Unknown($"Codex-Push-Aktualisierung ist ungültig (parse): {exception.Message}");
        }
        Volatile.Write(ref current, result);
        UsageChanged?.Invoke(this, new UsageChangedEventArgs(result));
    }

    private static string ToReason(CodexAppServerFailureKind kind) => kind switch
    {
        CodexAppServerFailureKind.Authentication => "authentication",
        CodexAppServerFailureKind.Connection => "connection",
        CodexAppServerFailureKind.Parse => "parse",
        _ => "protocol"
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        client.RateLimitsChanged -= OnRateLimitsChanged;
        refreshGate.Dispose();
    }
}
