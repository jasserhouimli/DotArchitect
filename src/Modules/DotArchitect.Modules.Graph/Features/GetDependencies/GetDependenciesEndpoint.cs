using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Graph.Algorithms;
using DotArchitect.Modules.Graph.Features.GetGraph;
using DotArchitect.Modules.Graph.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Graph.Features.GetDependencies;

public static class GetDependenciesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/analyses/{analysisId:guid}/projects/{projectId}/dependencies", async (
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

            var direct = graph.GetDependencies(nodeId);
            var transitive = GraphTraversal.GetTransitiveDependencies(nodeId, graph);

            var directNodes = direct
                .Where(id => graph.Nodes.ContainsKey(id))
                .Select(id => graph.Nodes[id])
                .ToList();

            var transitiveNodes = transitive
                .Where(id => graph.Nodes.ContainsKey(id))
                .Select(id => graph.Nodes[id])
                .ToList();

            return Results.Ok(new { direct = directNodes, transitive = transitiveNodes });
        })
        .RequireAuthorization()
        .WithName("GetDependencies")
        .WithTags("Graph");
    }
}
