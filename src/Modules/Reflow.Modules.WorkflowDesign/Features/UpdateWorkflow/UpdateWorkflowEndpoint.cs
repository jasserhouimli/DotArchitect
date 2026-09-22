using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.UpdateWorkflow;

public static class UpdateWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/v1/workflows/{workflowId:guid}", async (
            Guid workflowId,
            UpdateWorkflowRequest request,
            UpdateWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, request, ct);
            if (!result) return Results.NotFound(new { error = "Workflow not found" });
            return Results.Ok(new { message = "Workflow updated" });
        })
        .RequireAuthorization()
        .WithName("UpdateWorkflow")
        .WithTags("Workflows");
    }
}
