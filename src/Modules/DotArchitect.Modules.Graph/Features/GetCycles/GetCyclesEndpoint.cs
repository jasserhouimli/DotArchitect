using DotArchitect.Modules.Graph.Algorithms;
using DotArchitect.Modules.Graph.Features.GetGraph;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Graph.Features.GetCycles;

public static class GetCyclesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/analyses/{analysisId:guid}/cycles", async (
            Guid analysisId,
            GetGraphHandler handler,
            CancellationToken ct) =>
        {
            var graphResult = await handler.Handle(analysisId, ct);
            if (!graphResult.IsSuccess)
                return Results.NotFound(new { error = graphResult.Error });

            var graph = graphResult.Value!;
            var cycles = CycleDetector.DetectCycles(graph);

            var cycleDetails = cycles.Select(cycle => cycle
                .Select(id => graph.Nodes.TryGetValue(id, out var node) ? node.Name : id)
                .ToList()
            ).ToList();

            return Results.Ok(new { hasCycles = cycles.Count > 0, cycles = cycleDetails });
        })
        .RequireAuthorization()
        .WithName("GetCycles")
        .WithTags("Graph");
    }
}
