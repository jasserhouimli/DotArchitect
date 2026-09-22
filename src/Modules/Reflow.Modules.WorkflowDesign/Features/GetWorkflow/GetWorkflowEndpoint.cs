using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.GetWorkflow;

public static class GetWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}", async (
            Guid workflowId,
            GetWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (result is null) return Results.NotFound(new { error = "Workflow not found" });
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetWorkflow")
        .WithTags("Workflows");
    }
}
