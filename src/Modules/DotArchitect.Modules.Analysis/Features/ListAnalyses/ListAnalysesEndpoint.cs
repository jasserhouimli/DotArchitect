using DotArchitect.Modules.Analysis.Features.ListAnalyses;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Analysis.Features.ListAnalyses;

public static class ListAnalysesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workspaces/{workspaceId:guid}/analyses", async (
            Guid workspaceId,
            ListAnalysesHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(workspaceId, ct);
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("ListAnalyses")
        .WithTags("Analysis");
    }
}
