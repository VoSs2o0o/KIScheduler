using KIScheduler.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace KIScheduler.Infrastructure.Projects;

public static class ProjectServiceCollectionExtensions
{
    public static IServiceCollection AddKischedulerProjectServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IProjectRootResolver, ProjectRootResolver>();
        services.AddSingleton<IProjectCreationService, ProjectCreationService>();
        return services;
    }
}
