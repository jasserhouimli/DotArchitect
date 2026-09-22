using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.RetryTask;

public static class RetryTaskEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/tasks/{taskRunId:guid}/retry", async (
            Guid taskRunId, RetryTaskHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(taskRunId, userId, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Task queued for retry" });
        }).RequireAuthorization().WithName("RetryTask").WithTags("Tasks");
    }
}
