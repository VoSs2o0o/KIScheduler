using KIScheduler.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace KIScheduler.Platforms;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddKischedulerPlatformServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAiPlatformRegistry, AiPlatformRegistry>();
        services.AddSingleton<IUsageProviderRegistry, UsageProviderRegistry>();
        services.AddSingleton<IPlatformConfigurationValidator, PlatformConfigurationValidator>();
        return services;
    }

    public static IServiceCollection AddAiPlatform<TPlatform>(this IServiceCollection services)
        where TPlatform : class, IAiPlatform
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TPlatform>();
        services.AddSingleton<IAiPlatform>(provider => provider.GetRequiredService<TPlatform>());
        return services;
    }

    public static IServiceCollection AddUsageProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IUsageProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TProvider>();
        services.AddSingleton<IUsageProvider>(provider => provider.GetRequiredService<TProvider>());
        return services;
    }
}
