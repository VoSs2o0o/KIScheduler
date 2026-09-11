using KIScheduler.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace KIScheduler.Infrastructure.Processes;

public static class ProcessServiceCollectionExtensions
{
    public static IServiceCollection AddKischedulerProcessRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IProcessRunner, LocalProcessRunner>();
        return services;
    }
}
