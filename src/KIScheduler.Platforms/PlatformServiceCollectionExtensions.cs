using KIScheduler.Core.Contracts;
using KIScheduler.Platforms.Codex;
using Microsoft.Extensions.Configuration;
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

    public static IServiceCollection AddCodexPlatform(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<CodexOptions>(configuration.GetSection(CodexOptions.SectionName));
        services.AddSingleton<ICodexAppServerClient, CodexAppServerClient>();
        services.AddAiPlatform<CodexPlatform>();
        services.AddUsageProvider<CodexUsageProvider>();
        return services;
    }
}
