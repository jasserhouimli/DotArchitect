using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;

public static class GetWorkflowRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/runs/{runId:guid}", async (
            Guid runId, GetWorkflowRunHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var run = await handler.Handle(runId, userId, ct);
            if (run is null) return Results.NotFound(new { error = "Run not found" });
            return Results.Ok(run);
        }).RequireAuthorization().WithName("GetWorkflowRun").WithTags("Runs");
    }
}
