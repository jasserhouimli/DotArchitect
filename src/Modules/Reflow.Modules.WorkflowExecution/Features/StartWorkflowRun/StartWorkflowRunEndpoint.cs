using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public static class StartWorkflowRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/runs", async (
            Guid workflowId, StartWorkflowRunHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, userId, ct);
            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/runs/{result.Value}", new { id = result.Value }),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        }).RequireAuthorization().WithName("StartWorkflowRun").WithTags("Runs");
    }
}
