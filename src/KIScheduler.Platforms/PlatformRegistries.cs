using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms;

public sealed class AiPlatformRegistry : IAiPlatformRegistry
{
    private readonly IReadOnlyDictionary<string, IAiPlatform> platforms;

    public AiPlatformRegistry(IEnumerable<IAiPlatform> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        this.platforms = BuildUniqueMap(platforms, platform => platform.PlatformId, "Ausführungsadapter");
    }

    public IAiPlatform GetRequired(PlatformId platformId)
    {
        ArgumentNullException.ThrowIfNull(platformId);
        return TryGet(platformId, out var platform)
            ? platform!
            : throw new PlatformRegistrationException(
                $"Für Plattform '{platformId.Value}' ist kein Ausführungsadapter registriert.");
    }

    public bool TryGet(PlatformId platformId, out IAiPlatform? platform)
    {
        ArgumentNullException.ThrowIfNull(platformId);
        return platforms.TryGetValue(platformId.Value, out platform);
    }

    private static IReadOnlyDictionary<string, T> BuildUniqueMap<T>(IEnumerable<T> registrations,
        Func<T, PlatformId> idSelector, string registrationName)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations)
        {
            var id = idSelector(registration) ??
                throw new PlatformRegistrationException($"Ein {registrationName} besitzt keine Plattform-ID.");
            if (!result.TryAdd(id.Value, registration))
            {
                throw new PlatformRegistrationException(
                    $"Für Plattform '{id.Value}' ist mehr als ein {registrationName} registriert.");
            }
        }

        return result;
    }
}

public sealed class UsageProviderRegistry : IUsageProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IUsageProvider> providers;

    public UsageProviderRegistry(IEnumerable<IUsageProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var result = new Dictionary<string, IUsageProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (provider.PlatformId is null)
            {
                throw new PlatformRegistrationException("Ein Usage-Provider besitzt keine Plattform-ID.");
            }

            if (!result.TryAdd(provider.PlatformId.Value, provider))
            {
                throw new PlatformRegistrationException(
                    $"Für Plattform '{provider.PlatformId.Value}' ist mehr als ein Usage-Provider registriert.");
            }
        }

        this.providers = result;
    }

    public IUsageProvider GetRequired(PlatformId platformId)
    {
        ArgumentNullException.ThrowIfNull(platformId);
        return TryGet(platformId, out var provider)
            ? provider!
            : throw new PlatformRegistrationException(
                $"Für Plattform '{platformId.Value}' ist kein Usage-Provider registriert.");
    }

    public bool TryGet(PlatformId platformId, out IUsageProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(platformId);
        return providers.TryGetValue(platformId.Value, out provider);
    }
}
