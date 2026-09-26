using Microsoft.Extensions.DependencyInjection;
using Reflow.Infrastructure.Events;

namespace Reflow.Infrastructure;

public static class InfrastructureModule
{
    public static void AddReflowInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InProcessEventBus>();
    }
}
