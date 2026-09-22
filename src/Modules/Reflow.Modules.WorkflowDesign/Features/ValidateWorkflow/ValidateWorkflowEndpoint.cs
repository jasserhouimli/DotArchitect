using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.ValidateWorkflow;

public static class ValidateWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/validate", async (
            Guid workflowId,
            ValidateWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ValidateWorkflow")
        .WithTags("Workflows");
    }
}
