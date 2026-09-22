using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.CancelRun;

public static class CancelRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/runs/{runId:guid}/cancel", async (
            Guid runId, CancelRunHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(runId, userId, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Run cancelled" });
        }).RequireAuthorization().WithName("CancelWorkflowRun").WithTags("Runs");
    }
}
