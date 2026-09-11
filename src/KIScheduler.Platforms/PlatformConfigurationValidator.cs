using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms;

public sealed class PlatformConfigurationValidator : IPlatformConfigurationValidator
{
    private readonly IAiPlatformRegistry platformRegistry;
    private readonly IUsageProviderRegistry usageProviderRegistry;

    public PlatformConfigurationValidator(IAiPlatformRegistry platformRegistry,
        IUsageProviderRegistry usageProviderRegistry)
    {
        this.platformRegistry = platformRegistry ?? throw new ArgumentNullException(nameof(platformRegistry));
        this.usageProviderRegistry = usageProviderRegistry ?? throw new ArgumentNullException(nameof(usageProviderRegistry));
    }

    public void ValidateRegistrations(IEnumerable<PlatformDefinition> enabledPlatforms)
    {
        ArgumentNullException.ThrowIfNull(enabledPlatforms);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in enabledPlatforms)
        {
            if (!seen.Add(definition.Id.Value))
            {
                throw new PlatformConfigurationException(
                    $"Plattform '{definition.Id.Value}' ist mehrfach aktiviert.");
            }

            var platform = platformRegistry.GetRequired(definition.Id);
            var provider = usageProviderRegistry.GetRequired(definition.Id);
            EnsureSameId(definition.Id, platform.PlatformId, "Ausführungsadapter");
            EnsureSameId(definition.Id, provider.PlatformId, "Usage-Provider");
        }
    }

    public void ValidateExecution(PlatformDefinition platform, PlatformExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(request);
        EnsureSameId(platform.Id, request.PlatformId, "Ausführungsanfrage");
        _ = platformRegistry.GetRequired(request.PlatformId);

        var model = platform.Models.FirstOrDefault(candidate => candidate.Id == request.ModelId);
        if (model is null)
        {
            throw new PlatformConfigurationException(
                $"Modell '{request.ModelId.Value}' ist für Plattform '{platform.Id.Value}' nicht konfiguriert.");
        }

        if (!model.SupportedEfforts.Contains(request.Effort))
        {
            throw new PlatformConfigurationException(
                $"Effort '{request.Effort.Value}' wird vom Modell '{request.ModelId.Value}' nicht unterstützt.");
        }

        var adapter = platformRegistry.GetRequired(platform.Id);
        if (!string.IsNullOrWhiteSpace(request.SessionId) && !adapter.Capabilities.SupportsResume)
        {
            throw new PlatformConfigurationException(
                $"Plattform '{platform.Id.Value}' unterstützt keine Sitzungsfortsetzung.");
        }
    }

    public async Task<PlatformHealth> CheckHealthAsync(PlatformDefinition platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platform);
        var adapter = platformRegistry.GetRequired(platform.Id);
        var health = await adapter.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (!health.IsAvailable || health.SupportedModels.Count == 0)
        {
            return health;
        }

        foreach (var configuredModel in platform.Models)
        {
            var supportedModel = health.SupportedModels.FirstOrDefault(candidate => candidate.Id == configuredModel.Id);
            if (supportedModel is null)
            {
                return new PlatformHealth(PlatformHealthStatus.Misconfigured,
                    $"Das konfigurierte Modell '{configuredModel.Id.Value}' wird vom installierten Adapter nicht unterstützt.",
                    health.SupportedModels);
            }

            var unsupportedEffort = configuredModel.SupportedEfforts.FirstOrDefault(
                effort => !supportedModel.SupportedEfforts.Contains(effort));
            if (unsupportedEffort is not null)
            {
                return new PlatformHealth(PlatformHealthStatus.Misconfigured,
                    $"Effort '{unsupportedEffort.Value}' wird für Modell '{configuredModel.Id.Value}' nicht unterstützt.",
                    health.SupportedModels);
            }
        }

        return health;
    }

    private static void EnsureSameId(PlatformId expected, PlatformId actual, string component)
    {
        if (!string.Equals(expected.Value, actual.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformConfigurationException(
                $"{component} verweist auf Plattform '{actual.Value}', erwartet wurde '{expected.Value}'.");
        }
    }
}
