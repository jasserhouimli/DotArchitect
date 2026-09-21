using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotArchitect.Modules.Workspaces.Persistence;
using DotArchitect.Modules.Workspaces.Features.CreateWorkspace;
using DotArchitect.Modules.Workspaces.Features.ListWorkspaces;
using DotArchitect.Modules.Workspaces.Features.GetWorkspace;
using DotArchitect.Modules.Workspaces.Features.RenameWorkspace;
using DotArchitect.Modules.Workspaces.Features.DeleteWorkspace;

namespace DotArchitect.Modules.Workspaces;

public static class WorkspacesModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DotArchitect");

        builder.Services.AddDbContext<WorkspacesDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<WorkspacesDbContext>();

        builder.Services.AddScoped<IValidator<CreateWorkspaceRequest>, CreateWorkspaceRequestValidator>();
        builder.Services.AddScoped<IValidator<RenameWorkspaceRequest>, RenameWorkspaceRequestValidator>();

        builder.Services.AddScoped<CreateWorkspaceHandler>();
        builder.Services.AddScoped<ListWorkspacesHandler>();
        builder.Services.AddScoped<GetWorkspaceHandler>();
        builder.Services.AddScoped<RenameWorkspaceHandler>();
        builder.Services.AddScoped<DeleteWorkspaceHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateWorkspaceEndpoint.Map(app);
        ListWorkspacesEndpoint.Map(app);
        GetWorkspaceEndpoint.Map(app);
        RenameWorkspaceEndpoint.Map(app);
        DeleteWorkspaceEndpoint.Map(app);
    }
}
