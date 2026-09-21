using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DotArchitect.Modules.Analysis.Persistence;
using DotArchitect.Modules.Graph.Features.GetGraph;
using DotArchitect.Modules.Graph.Features.GetDependencies;
using DotArchitect.Modules.Graph.Features.GetDependents;
using DotArchitect.Modules.Graph.Features.GetImpact;
using DotArchitect.Modules.Graph.Features.GetCycles;

namespace DotArchitect.Modules.Graph;

public static class GraphModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<GetGraphHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        GetGraphEndpoint.Map(app);
        GetDependenciesEndpoint.Map(app);
        GetDependentsEndpoint.Map(app);
        GetImpactEndpoint.Map(app);
        GetCyclesEndpoint.Map(app);
    }
}
