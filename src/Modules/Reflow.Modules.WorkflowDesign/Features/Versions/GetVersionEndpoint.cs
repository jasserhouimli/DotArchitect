using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.Versions;

public static class GetVersionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}/versions/{versionNumber:int}", async (
            Guid workflowId,
            int versionNumber,
            GetVersionHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, versionNumber, ownerId, ct);
            if (result is null) return Results.NotFound(new { error = "Version not found" });
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetWorkflowVersion")
        .WithTags("Workflows");
    }
}
