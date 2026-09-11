using KIScheduler.Core.Contracts;
using KIScheduler.Platforms.Claude;
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
        var section = configuration.GetSection(CodexOptions.SectionName);
        services.AddOptions<CodexOptions>().Configure(options =>
        {
            section.Bind(options);

            // The configuration binder appends indexed values to initialized
            // collections. Replace this collection explicitly so the default
            // app-server command is not duplicated.
            var configuredArguments = section
                .GetSection(nameof(CodexOptions.AppServerArguments))
                .Get<string[]>();
            if (configuredArguments is { Length: > 0 })
                options.AppServerArguments = [.. configuredArguments];
        });
        services.AddSingleton<ICodexAppServerClient, CodexAppServerClient>();
        services.AddAiPlatform<CodexPlatform>();
        services.AddUsageProvider<CodexUsageProvider>();
        return services;
    }

    public static IServiceCollection AddClaudePlatform(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));
        services.AddSingleton<CommandRegexReader>();
        services.AddAiPlatform<ClaudePlatform>();
        services.AddUsageProvider<ClaudeUsageProvider>();
        return services;
    }
}
