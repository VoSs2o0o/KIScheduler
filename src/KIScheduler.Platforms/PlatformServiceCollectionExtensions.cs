using KIScheduler.Core.Contracts;
using KIScheduler.Platforms.Claude;
using KIScheduler.Platforms.Codex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<ICodexProfileResolver, CodexProfileResolver>();
        services.AddSingleton<ICodexAppServerClientFactory, CodexAppServerClientFactory>();
        services.AddAiPlatform<CodexPlatform>();
        services.AddUsageProvider<CodexUsageProvider>();
        return services;
    }

    public static IServiceCollection AddClaudePlatform(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ClaudeOptions.SectionName);
        services.AddOptions<ClaudeOptions>().Configure(options =>
        {
            section.Bind(options);

            var configuredUsageArguments = section
                .GetSection($"{nameof(ClaudeOptions.Usage)}:{nameof(ClaudeUsageOptions.Arguments)}")
                .Get<string[]>();
            if (configuredUsageArguments is { Length: > 0 })
                options.Usage.Arguments = [.. configuredUsageArguments];
        });
        services.AddSingleton<IClaudeProfileResolver, ClaudeProfileResolver>();
        services.AddSingleton<CommandRegexReader>();
        services.AddAiPlatform<ClaudePlatform>();
        services.AddSingleton<ClaudeUsageProvider>(provider => new ClaudeUsageProvider(
            provider.GetRequiredService<CommandRegexReader>(),
            provider.GetRequiredService<IClaudeProfileResolver>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IOptions<ClaudeOptions>>(),
            provider.GetService<IPlatformRepository>()));
        services.AddSingleton<IUsageProvider>(provider => provider.GetRequiredService<ClaudeUsageProvider>());
        return services;
    }
}
