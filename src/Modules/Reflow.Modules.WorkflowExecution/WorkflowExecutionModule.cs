using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;
using Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;
using Reflow.Modules.WorkflowExecution.Features.ListWorkflowRuns;
using Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;
using Reflow.Modules.WorkflowExecution.Features.CancelRun;
using Reflow.Modules.WorkflowExecution.Features.RetryTask;
using Reflow.Modules.WorkflowExecution.Features.GetTaskRun;
using Reflow.Modules.WorkflowExecution.Features.Artifacts;
using Reflow.Modules.WorkflowExecution.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Reflow.Modules.WorkflowExecution;

public static class WorkflowExecutionModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");

        builder.Services.AddDbContext<WorkflowExecutionDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddScoped<WorkflowExecutionDbContext>();

        builder.Services.AddHttpClient("reflow-http", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(130);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        });

        builder.Services.AddSingleton<IArtifactStore, LocalArtifactStore>();

        builder.Services.AddSingleton<ITaskHandler, CsvReadHandler>();
        builder.Services.AddSingleton<ITaskHandler, ValidateHandler>();
        builder.Services.AddSingleton<ITaskHandler, FilterHandler>();
        builder.Services.AddSingleton<ITaskHandler, TransformHandler>();
        builder.Services.AddSingleton<ITaskHandler, AggregateHandler>();
        builder.Services.AddSingleton<ITaskHandler, OutputHandler>();
        builder.Services.AddSingleton<ITaskHandler, HttpRequestHandler>();
        builder.Services.AddSingleton<TaskHandlerRegistry>(sp =>
            new TaskHandlerRegistry(sp.GetServices<ITaskHandler>()));

        builder.Services.AddScoped<StartWorkflowRunHandler>();
        builder.Services.AddScoped<GetWorkflowRunHandler>();
        builder.Services.AddScoped<ListWorkflowRunsHandler>();
        builder.Services.AddScoped<GetTaskRunsHandler>();
        builder.Services.AddScoped<CancelRunHandler>();
        builder.Services.AddScoped<RetryTaskHandler>();
        builder.Services.AddScoped<GetTaskRunHandler>();
        builder.Services.AddScoped<GetArtifactHandler>();

        builder.Services.AddHostedService<WorkflowWorker>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        StartWorkflowRunEndpoint.Map(app);
        GetWorkflowRunEndpoint.Map(app);
        ListWorkflowRunsEndpoint.Map(app);
        GetTaskRunsEndpoint.Map(app);
        CancelRunEndpoint.Map(app);
        RetryTaskEndpoint.Map(app);
        GetTaskRunEndpoint.Map(app);
        GetArtifactEndpoint.Map(app);
    }
}
