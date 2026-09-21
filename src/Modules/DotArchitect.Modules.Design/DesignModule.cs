using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotArchitect.Modules.Design.Persistence;
using DotArchitect.Modules.Design.Features.CreateDesign;
using DotArchitect.Modules.Design.Features.ListDesigns;
using DotArchitect.Modules.Design.Features.GetDesign;
using DotArchitect.Modules.Design.Features.AddProject;
using DotArchitect.Modules.Design.Features.UpdateProject;
using DotArchitect.Modules.Design.Features.RemoveProject;
using DotArchitect.Modules.Design.Features.AddReference;
using DotArchitect.Modules.Design.Features.RemoveReference;
using DotArchitect.Modules.Design.Features.ValidateDesign;
using DotArchitect.Modules.Design.Features.GenerateSolution;

namespace DotArchitect.Modules.Design;

public static class DesignModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DotArchitect");

        builder.Services.AddDbContext<DesignDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<DesignDbContext>();
        builder.Services.AddScoped<CreateDesignHandler>();
        builder.Services.AddScoped<ListDesignsHandler>();
        builder.Services.AddScoped<GetDesignHandler>();
        builder.Services.AddScoped<AddProjectHandler>();
        builder.Services.AddScoped<UpdateProjectHandler>();
        builder.Services.AddScoped<RemoveProjectHandler>();
        builder.Services.AddScoped<AddReferenceHandler>();
        builder.Services.AddScoped<RemoveReferenceHandler>();
        builder.Services.AddScoped<ValidateDesignHandler>();
        builder.Services.AddScoped<GenerateSolutionHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateDesignEndpoint.Map(app);
        ListDesignsEndpoint.Map(app);
        GetDesignEndpoint.Map(app);
        AddProjectEndpoint.Map(app);
        UpdateProjectEndpoint.Map(app);
        RemoveProjectEndpoint.Map(app);
        AddReferenceEndpoint.Map(app);
        RemoveReferenceEndpoint.Map(app);
        ValidateDesignEndpoint.Map(app);
        GenerateSolutionEndpoint.Map(app);
    }
}
