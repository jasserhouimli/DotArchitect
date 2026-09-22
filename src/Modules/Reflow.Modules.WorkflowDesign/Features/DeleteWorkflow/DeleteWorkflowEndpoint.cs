using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.DeleteWorkflow;

public static class DeleteWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/workflows/{workflowId:guid}", async (
            Guid workflowId,
            DeleteWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (!result) return Results.NotFound(new { error = "Workflow not found" });
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeleteWorkflow")
        .WithTags("Workflows");
    }
}
