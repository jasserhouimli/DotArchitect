using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.WorkflowExecution.Persistence;

namespace Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;

public static class GetTaskRunsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/runs/{runId:guid}/tasks", async (
            Guid runId, GetTaskRunsHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tasks = await handler.Handle(runId, userId, ct);
            return Results.Ok(tasks);
        }).RequireAuthorization().WithName("GetTaskRuns").WithTags("Runs");

        app.MapGet("/api/v1/runs/{runId:guid}/logs", async (
            Guid runId, GetTaskRunsHandler handler, WorkflowExecutionDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
            if (run is null) return Results.NotFound(new { error = "Run not found" });
            var logs = await db.ExecutionLogs.Where(l => l.WorkflowRunId == runId).OrderBy(l => l.Timestamp).ToListAsync(ct);
            return Results.Ok(logs);
        }).RequireAuthorization().WithName("GetRunLogs").WithTags("Runs");
    }
}
