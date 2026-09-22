using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.CancelRun;

public class CancelRunHandler(WorkflowExecutionDbContext db)
{
    public async Task<Result> Handle(Guid runId, Guid userId, CancellationToken ct)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
        if (run is null) return Result.Failure("Run not found", 404);

        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
            return Result.Failure($"Run is already {run.Status}", 400);

        run.Status = WorkflowRunStatus.Cancelled;
        run.CompletedAt = DateTime.UtcNow;

        var pending = await db.TaskRuns
            .Where(t => t.WorkflowRunId == runId
                && (t.Status == TaskRunStatus.Pending || t.Status == TaskRunStatus.Ready || t.Status == TaskRunStatus.RetryScheduled))
            .ToListAsync(ct);

        foreach (var task in pending)
        {
            task.Status = TaskRunStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
        }

        db.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(), WorkflowRunId = run.Id,
            Message = $"Run {run.Id} cancelled by user", Level = "Warning"
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(200);
    }
}
