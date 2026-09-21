using DotArchitect.Modules.Graph.Algorithms;
using DotArchitect.Modules.Graph.Features.GetGraph;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Graph.Features.GetImpact;

public static class GetImpactEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/analyses/{analysisId:guid}/projects/{projectId}/impact", async (
            Guid analysisId,
            Guid projectId,
            GetGraphHandler handler,
            CancellationToken ct) =>
        {
            var graphResult = await handler.Handle(analysisId, ct);
            if (!graphResult.IsSuccess)
                return Results.NotFound(new { error = graphResult.Error });

            var graph = graphResult.Value!;
            var nodeId = projectId.ToString();

            if (!graph.Nodes.ContainsKey(nodeId))
                return Results.NotFound(new { error = "Project not found." });

            var impacted = GraphTraversal.GetImpact(nodeId, graph);
            var impactedNodes = impacted
                .Where(id => graph.Nodes.ContainsKey(id))
                .Select(id => graph.Nodes[id])
                .ToList();

            return Results.Ok(new
            {
                message = "These are structural estimates, not guarantees.",
                impactedProjects = impactedNodes
            });
        })
        .RequireAuthorization()
        .WithName("GetImpact")
        .WithTags("Graph");
    }
}
