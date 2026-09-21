using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.ListDesigns;

public static class ListDesignsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workspaces/{workspaceId:guid}/designs", async (
            Guid workspaceId,
            ListDesignsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(workspaceId, ct);
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("ListDesigns")
        .WithTags("Design");
    }
}
