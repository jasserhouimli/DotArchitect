using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.ListWorkflowRuns;

public static class ListWorkflowRunsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}/runs", async (
            Guid workflowId, ListWorkflowRunsHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var runs = await handler.Handle(workflowId, userId, ct);
            return Results.Ok(runs);
        }).RequireAuthorization().WithName("ListWorkflowRuns").WithTags("Runs");
    }
}
