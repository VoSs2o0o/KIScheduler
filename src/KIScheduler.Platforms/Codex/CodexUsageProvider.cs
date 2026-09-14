using System.Collections.Concurrent;
using System.Text.Json;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Codex;

public sealed class CodexUsageProvider : IUsageProvider, IDisposable
{
    private const string Source = "codex-app-server/account/rateLimits/read";
    private readonly ICodexAppServerClientFactory? clientFactory;
    private readonly ICodexProfileResolver? profileResolver;
    private readonly ICodexAppServerClient? directClient;
    private readonly IClock clock;
    private readonly CodexOptions options;
    private readonly ConcurrentDictionary<PlatformProfileId, UsageReadResult> current = new();
    private readonly ConcurrentDictionary<PlatformProfileId, SemaphoreSlim> refreshGates = new();
    private readonly Dictionary<PlatformProfileId, ICodexAppServerClient> subscriptions = [];
    private readonly object subscriptionsGate = new();
    private UsageReadResult legacyCurrent = UsageReadResult.Unknown("Codex-Usage wurde noch nicht gelesen.");
    private bool disposed;

    public CodexUsageProvider(ICodexAppServerClientFactory clientFactory, ICodexProfileResolver profileResolver,
        IClock clock, IOptions<CodexOptions> options)
    {
        this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        this.profileResolver = profileResolver ?? throw new ArgumentNullException(nameof(profileResolver));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public CodexUsageProvider(ICodexAppServerClient client, IClock clock, IOptions<CodexOptions> options)
    {
        directClient = client ?? throw new ArgumentNullException(nameof(client));
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
        if (profileResolver is null)
            return await ReadLegacyAsync(forceRefresh, cancellationToken).ConfigureAwait(false);

        try
        {
            PlatformProfile profile = await profileResolver.ResolveDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return await ReadAsync(profile.Id, forceRefresh, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexProfileException exception)
        {
            return UsageReadResult.Unknown($"Codex-Usage unbekannt (profile): {exception.Message}");
        }
    }

    public async Task<UsageReadResult> ReadAsync(PlatformProfileId profileId, bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (clientFactory is null)
            throw new InvalidOperationException("Dieser Codex-Usage-Provider besitzt keine profilbezogene Client-Factory.");

        UsageReadResult cached = current.GetOrAdd(profileId,
            static id => UsageReadResult.Unknown("Codex-Usage wurde noch nicht gelesen.").ForProfile(id));
        if (!forceRefresh && IsFresh(cached)) return cached;

        SemaphoreSlim refreshGate = refreshGates.GetOrAdd(profileId, static _ => new SemaphoreSlim(1, 1));
        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = current[profileId];
            if (!forceRefresh && IsFresh(cached)) return cached;

            ICodexAppServerClient client = await clientFactory.GetAsync(profileId, cancellationToken)
                .ConfigureAwait(false);
            Subscribe(profileId, client);
            return await ReadCoreAsync(client, profileId, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexProfileException exception)
        {
            UsageReadResult unknown = UsageReadResult.Unknown(
                $"Codex-Usage unbekannt (profile): {exception.Message}").ForProfile(profileId);
            current[profileId] = unknown;
            return unknown;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<UsageReadResult> ReadLegacyAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        UsageReadResult cached = Volatile.Read(ref legacyCurrent);
        if (!forceRefresh && IsFresh(cached)) return cached;
        ICodexAppServerClient client = directClient
            ?? throw new InvalidOperationException("Kein Codex-App-Server-Client konfiguriert.");
        UsageReadResult result = await ReadCoreAsync(client, client.PlatformProfileId, cancellationToken)
            .ConfigureAwait(false);
        Volatile.Write(ref legacyCurrent, result);
        return result;
    }

    private async Task<UsageReadResult> ReadCoreAsync(ICodexAppServerClient client,
        PlatformProfileId? profileId, CancellationToken cancellationToken)
    {
        try
        {
            CodexRateLimitsResponse response = await client.ReadRateLimitsAsync(cancellationToken)
                .ConfigureAwait(false);
            UsageReadResult normalized = Normalize(response, clock.UtcNow, profileId);
            Store(profileId, normalized);
            return normalized;
        }
        catch (CodexAppServerException exception)
        {
            UsageReadResult unknown = UsageReadResult.Unknown(
                $"Codex-Usage unbekannt ({ToReason(exception.Kind)}): {exception.Message}");
            if (profileId.HasValue) unknown = unknown.ForProfile(profileId.Value);
            Store(profileId, unknown);
            return unknown;
        }
        catch (Exception exception) when (exception is JsonException or FormatException
            or OverflowException or ArgumentOutOfRangeException)
        {
            UsageReadResult unknown = UsageReadResult.Unknown(
                $"Codex-Usage unbekannt (parse): {exception.Message}");
            if (profileId.HasValue) unknown = unknown.ForProfile(profileId.Value);
            Store(profileId, unknown);
            return unknown;
        }
    }

    public static UsageReadResult Normalize(CodexRateLimitsResponse response, DateTimeOffset readAtUtc,
        PlatformProfileId? profileId = null)
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

        UsageReadResult result;
        if (windows.Count == 0)
            result = UsageReadResult.Unknown(missing.Count == 0
                ? "Codex-Rate-Limit-Antwort enthält keine Usage-Fenster."
                : $"Codex-Rate-Limit-Antwort enthält keine nutzbaren Fenster; fehlt: {string.Join(", ", missing)}.");
        else
        {
            var snapshot = new UsageSnapshot(CodexPlatform.Id, readAtUtc, Source, UsageQuality.Aktuell, windows);
            result = new UsageReadResult(UsageReadStatus.Available, snapshot,
                missing.Count == 0 ? null : $"Fehlende Codex-Usage-Felder: {string.Join(", ", missing)}.",
                profileId);
        }
        return profileId.HasValue && !result.PlatformProfileId.HasValue ? result.ForProfile(profileId.Value) : result;

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

    private void Subscribe(PlatformProfileId profileId, ICodexAppServerClient client)
    {
        lock (subscriptionsGate)
        {
            if (subscriptions.TryGetValue(profileId, out ICodexAppServerClient? previous))
            {
                if (ReferenceEquals(previous, client)) return;
                previous.RateLimitsChanged -= OnRateLimitsChanged;
            }
            client.RateLimitsChanged += OnRateLimitsChanged;
            subscriptions[profileId] = client;
        }
    }

    private void OnRateLimitsChanged(object? sender, CodexRateLimitsChangedEventArgs args)
    {
        PlatformProfileId? profileId = args.PlatformProfileId ?? (sender as ICodexAppServerClient)?.PlatformProfileId;
        UsageReadResult result;
        try
        {
            result = Normalize(args.RateLimits, clock.UtcNow, profileId);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException
            or ArgumentOutOfRangeException)
        {
            result = UsageReadResult.Unknown($"Codex-Push-Aktualisierung ist ungültig (parse): {exception.Message}");
            if (profileId.HasValue) result = result.ForProfile(profileId.Value);
        }
        Store(profileId, result);
        UsageChanged?.Invoke(this, new UsageChangedEventArgs(result));
    }

    private void Store(PlatformProfileId? profileId, UsageReadResult result)
    {
        if (profileId.HasValue) current[profileId.Value] = result;
        else Volatile.Write(ref legacyCurrent, result);
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
        if (directClient is not null) directClient.RateLimitsChanged -= OnRateLimitsChanged;
        lock (subscriptionsGate)
        {
            foreach (ICodexAppServerClient client in subscriptions.Values)
                client.RateLimitsChanged -= OnRateLimitsChanged;
            subscriptions.Clear();
        }
        foreach (SemaphoreSlim refreshGate in refreshGates.Values) refreshGate.Dispose();
    }
}
