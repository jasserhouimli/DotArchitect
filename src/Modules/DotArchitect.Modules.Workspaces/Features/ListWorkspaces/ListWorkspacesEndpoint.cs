using System.Security.Claims;
using DotArchitect.Modules.Workspaces.Features.ListWorkspaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotArchitect.Modules.Workspaces.Features.ListWorkspaces;

public static class ListWorkspacesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workspaces", async (
            [AsParameters] ListWorkspacesQuery query,
            ListWorkspacesHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(query, ownerId, ct);
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("ListWorkspaces")
        .WithTags("Workspaces");
    }
}
