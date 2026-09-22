using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.ArchiveWorkflow;

public static class ArchiveWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/archive", async (
            Guid workflowId,
            ArchiveWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Workflow archived" });
        })
        .RequireAuthorization()
        .WithName("ArchiveWorkflow")
        .WithTags("Workflows");
    }
}
