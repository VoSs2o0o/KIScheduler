using KIScheduler.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace KIScheduler.Infrastructure.Git;

public static class GitServiceCollectionExtensions
{
    public static IServiceCollection AddKischedulerGit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IGitService, GitService>();
        return services;
    }
}
