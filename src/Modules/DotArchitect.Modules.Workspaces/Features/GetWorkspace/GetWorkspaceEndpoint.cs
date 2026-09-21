using System.Security.Claims;
using DotArchitect.Modules.Workspaces.Features.GetWorkspace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Workspaces.Features.GetWorkspace;

public static class GetWorkspaceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workspaces/{workspaceId:guid}", async (
            Guid workspaceId,
            GetWorkspaceHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(new GetWorkspaceQuery(workspaceId), ownerId, ct);

            return result.StatusCode switch
            {
                200 => Results.Ok(result.Value),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("GetWorkspace")
        .WithTags("Workspaces");
    }
}
