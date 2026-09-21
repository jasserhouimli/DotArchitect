using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Graph.Algorithms;
using DotArchitect.Modules.Graph.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Graph.Features.GetGraph;

public static class GetGraphEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/analyses/{analysisId:guid}/graph", async (
            Guid analysisId,
            GetGraphHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(analysisId, ct);
            if (!result.IsSuccess)
                return Results.NotFound(new { error = result.Error });

            var graph = result.Value!;
            var nodes = graph.Nodes.Values.ToList();
            var edges = graph.Edges.ToList();

            return Results.Ok(new { nodes, edges });
        })
        .RequireAuthorization()
        .WithName("GetGraph")
        .WithTags("Graph");
    }
}
