using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.GetTaskRun;

public static class GetTaskRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/tasks/{taskRunId:guid}", async (
            Guid taskRunId, GetTaskRunHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var task = await handler.GetTaskAsync(taskRunId, userId, ct);
            if (task is null) return Results.NotFound(new { error = "Task not found" });
            return Results.Ok(task);
        }).RequireAuthorization().WithName("GetTaskRun").WithTags("Tasks");

        app.MapGet("/api/v1/tasks/{taskRunId:guid}/attempts", async (
            Guid taskRunId, GetTaskRunHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var attempts = await handler.GetAttemptsAsync(taskRunId, userId, ct);
            if (attempts is null) return Results.NotFound(new { error = "Task not found" });
            return Results.Ok(attempts);
        }).RequireAuthorization().WithName("GetTaskAttempts").WithTags("Tasks");
    }
}
