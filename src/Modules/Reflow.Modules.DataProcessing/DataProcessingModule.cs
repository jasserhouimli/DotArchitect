using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Reflow.Modules.DataProcessing;

public static class DataProcessingModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("reflow-http", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(130);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        });

        builder.Services.AddSingleton<ITaskHandler, CsvReadHandler>();
        builder.Services.AddSingleton<ITaskHandler, JsonReadHandler>();
        builder.Services.AddSingleton<ITaskHandler, ValidateHandler>();
        builder.Services.AddSingleton<ITaskHandler, FilterHandler>();
        builder.Services.AddSingleton<ITaskHandler, TransformHandler>();
        builder.Services.AddSingleton<ITaskHandler, AggregateHandler>();
        builder.Services.AddSingleton<ITaskHandler, SortHandler>();
        builder.Services.AddSingleton<ITaskHandler, LimitHandler>();
        builder.Services.AddSingleton<ITaskHandler, DedupeHandler>();
        builder.Services.AddSingleton<ITaskHandler, JoinHandler>();
        builder.Services.AddSingleton<ITaskHandler, ProfileHandler>();
        builder.Services.AddSingleton<ITaskHandler, TriggerPayloadHandler>();
        builder.Services.AddSingleton<ITaskHandler, OutputHandler>();
        builder.Services.AddSingleton<ITaskHandler, HttpRequestHandler>();
        builder.Services.AddSingleton<TaskHandlerRegistry>(sp =>
            new TaskHandlerRegistry(sp.GetServices<ITaskHandler>()));
    }
}
