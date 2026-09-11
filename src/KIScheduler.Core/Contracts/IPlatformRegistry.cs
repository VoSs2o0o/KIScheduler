using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public interface IAiPlatformRegistry
{
    IAiPlatform GetRequired(PlatformId platformId);
    bool TryGet(PlatformId platformId, out IAiPlatform? platform);
}

public interface IUsageProviderRegistry
{
    IUsageProvider GetRequired(PlatformId platformId);
    bool TryGet(PlatformId platformId, out IUsageProvider? provider);
}

public interface IPlatformConfigurationValidator
{
    void ValidateRegistrations(IEnumerable<PlatformDefinition> enabledPlatforms);
    void ValidateExecution(PlatformDefinition platform, PlatformExecutionRequest request);
    Task<PlatformHealth> CheckHealthAsync(PlatformDefinition platform,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformRegistrationException : InvalidOperationException
{
    public PlatformRegistrationException(string message) : base(message) { }
}

public sealed class PlatformConfigurationException : InvalidOperationException
{
    public PlatformConfigurationException(string message) : base(message) { }
}
