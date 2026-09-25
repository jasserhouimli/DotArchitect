using Microsoft.Extensions.DependencyInjection;
using Reflow.Infrastructure.Events;
using Reflow.Infrastructure.Realtime;
using Reflow.Infrastructure.Storage;

namespace Reflow.Infrastructure;

public static class InfrastructureModule
{
    public static void AddReflowInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IUploadStore, LocalUploadStore>();
        services.AddSingleton<IArtifactStore, LocalArtifactStore>();
        services.AddSingleton<IEventBus, InProcessEventBus>();
        services.AddScoped<IEventHandler<TaskChangedEvent>, RealtimeBridge>();
        services.AddScoped<IEventHandler<LogRecordedEvent>, RealtimeBridge>();
        services.AddScoped<IEventHandler<RunProgressEvent>, RealtimeBridge>();
        services.AddScoped<IEventHandler<RunFinishedEvent>, RealtimeBridge>();
        services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });
    }
}
