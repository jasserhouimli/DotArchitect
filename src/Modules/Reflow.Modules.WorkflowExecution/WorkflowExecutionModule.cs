using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;
using Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;
using Reflow.Modules.WorkflowExecution.Features.ListWorkflowRuns;
using Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;
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

        // Handlers registry
        builder.Services.AddSingleton<TaskHandlerRegistry>(sp =>
            new TaskHandlerRegistry(new ITaskHandler[]
            {
                new CsvReadHandler(), new ValidateHandler(), new FilterHandler(),
                new TransformHandler(), new AggregateHandler(), new OutputHandler(),
                new HttpRequestHandler()
            }));

        builder.Services.AddScoped<StartWorkflowRunHandler>();
        builder.Services.AddScoped<GetWorkflowRunHandler>();
        builder.Services.AddScoped<ListWorkflowRunsHandler>();
        builder.Services.AddScoped<GetTaskRunsHandler>();

        builder.Services.AddHostedService<WorkflowWorker>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        StartWorkflowRunEndpoint.Map(app);
        GetWorkflowRunEndpoint.Map(app);
        ListWorkflowRunsEndpoint.Map(app);
        GetTaskRunsEndpoint.Map(app);
    }
}
