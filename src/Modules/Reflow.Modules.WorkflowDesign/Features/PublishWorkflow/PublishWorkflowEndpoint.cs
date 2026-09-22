using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.PublishWorkflow;

public static class PublishWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/publish", async (
            Guid workflowId,
            PublishWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (!result.IsSuccess)
                return Results.BadRequest(new { error = result.Error });
            return Results.Ok(new { version = result.Value });
        })
        .RequireAuthorization()
        .WithName("PublishWorkflow")
        .WithTags("Workflows");
    }
}
