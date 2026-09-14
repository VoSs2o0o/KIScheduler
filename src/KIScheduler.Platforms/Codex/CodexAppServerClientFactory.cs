using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Codex;

public interface ICodexAppServerClientFactory : IAsyncDisposable
{
    Task<ICodexAppServerClient> GetAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default);
    Task InvalidateAsync(PlatformProfileId profileId);
}

public sealed class CodexAppServerClientFactory : ICodexAppServerClientFactory
{
    private readonly ICodexProfileResolver profiles;
    private readonly IOptions<CodexOptions> options;
    private readonly ILogger<CodexAppServerClient> logger;
    private readonly IPlatformRepository? platformRepository;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<PlatformProfileId, Entry> entries = [];
    private bool disposed;

    public CodexAppServerClientFactory(ICodexProfileResolver profiles, IOptions<CodexOptions> options,
        ILogger<CodexAppServerClient> logger, IPlatformRepository? platformRepository = null)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.platformRepository = platformRepository;
    }

    public async Task<ICodexAppServerClient> GetAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PlatformProfile profile;
        try
        {
            profile = await profiles.ResolveAsync(profileId, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexProfileException)
        {
            await InvalidateAsync(profileId).ConfigureAwait(false);
            throw;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (entries.TryGetValue(profileId, out Entry? current))
            {
                if (string.Equals(current.ConfigurationDirectory, profile.ConfigurationDirectory,
                        StringComparison.OrdinalIgnoreCase))
                    return current.Client;

                entries.Remove(profileId);
                await current.Client.DisposeAsync().ConfigureAwait(false);
            }

            var client = new CodexAppServerClient(profile, options, logger, platformRepository);
            entries.Add(profileId, new Entry(profile.ConfigurationDirectory, client));
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task InvalidateAsync(PlatformProfileId profileId)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!entries.Remove(profileId, out Entry? entry)) return;
            await entry.Client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (Entry entry in entries.Values)
                await entry.Client.DisposeAsync().ConfigureAwait(false);
            entries.Clear();
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private sealed record Entry(string ConfigurationDirectory, ICodexAppServerClient Client);
}
