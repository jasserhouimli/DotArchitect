using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Reflow.Infrastructure.Snapshots;
using Reflow.Modules.WorkflowDesign.Features.Snapshots;using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Features.CreateWorkflow;
using Reflow.Modules.WorkflowDesign.Features.ListWorkflows;
using Reflow.Modules.WorkflowDesign.Features.GetWorkflow;
using Reflow.Modules.WorkflowDesign.Features.UpdateWorkflow;
using Reflow.Modules.WorkflowDesign.Features.DeleteWorkflow;
using Reflow.Modules.WorkflowDesign.Features.PublishWorkflow;
using Reflow.Modules.WorkflowDesign.Features.ValidateWorkflow;
using Reflow.Modules.WorkflowDesign.Features.ArchiveWorkflow;
using Reflow.Modules.WorkflowDesign.Features.Versions;
using Reflow.Modules.WorkflowDesign.Features.Files;

namespace Reflow.Modules.WorkflowDesign;

public static class WorkflowDesignModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");

        builder.Services.AddDbContext<WorkflowDesignDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<WorkflowDesignDbContext>();

        builder.Services.AddScoped<IValidator<CreateWorkflowRequest>, CreateWorkflowRequestValidator>();

        builder.Services.AddScoped<CreateWorkflowHandler>();
        builder.Services.AddScoped<ListWorkflowsHandler>();
        builder.Services.AddScoped<GetWorkflowHandler>();
        builder.Services.AddScoped<UpdateWorkflowHandler>();
        builder.Services.AddScoped<DeleteWorkflowHandler>();
        builder.Services.AddScoped<PublishWorkflowHandler>();
        builder.Services.AddScoped<ValidateWorkflowHandler>();
        builder.Services.AddScoped<ArchiveWorkflowHandler>();
        builder.Services.AddScoped<ListVersionsHandler>();
        builder.Services.AddScoped<GetVersionHandler>();
        builder.Services.AddScoped<IWorkflowSnapshotProvider, WorkflowSnapshotProvider>();
        builder.Services.AddScoped<UploadWorkflowFileHandler>();
        builder.Services.AddScoped<ListWorkflowFilesHandler>();
        builder.Services.AddScoped<DeleteWorkflowFileHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateWorkflowEndpoint.Map(app);
        ListWorkflowsEndpoint.Map(app);
        GetWorkflowEndpoint.Map(app);
        UpdateWorkflowEndpoint.Map(app);
        DeleteWorkflowEndpoint.Map(app);
        PublishWorkflowEndpoint.Map(app);
        ValidateWorkflowEndpoint.Map(app);
        ArchiveWorkflowEndpoint.Map(app);
        ListVersionsEndpoint.Map(app);
        GetVersionEndpoint.Map(app);
        WorkflowFilesEndpoint.Map(app);
    }
}
