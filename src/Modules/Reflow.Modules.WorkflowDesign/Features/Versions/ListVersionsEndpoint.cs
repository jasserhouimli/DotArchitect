using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.Versions;

public static class ListVersionsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}/versions", async (
            Guid workflowId,
            ListVersionsHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (result is null) return Results.NotFound(new { error = "Workflow not found" });
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ListWorkflowVersions")
        .WithTags("Workflows");
    }
}
