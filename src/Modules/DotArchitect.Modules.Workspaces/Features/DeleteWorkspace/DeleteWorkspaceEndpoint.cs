using System.Security.Claims;
using DotArchitect.Modules.Workspaces.Features.DeleteWorkspace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Workspaces.Features.DeleteWorkspace;

public static class DeleteWorkspaceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/workspaces/{workspaceId:guid}", async (
            Guid workspaceId,
            DeleteWorkspaceHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workspaceId, ownerId, ct);

            return result.StatusCode switch
            {
                204 => Results.NoContent(),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("DeleteWorkspace")
        .WithTags("Workspaces");
    }
}
