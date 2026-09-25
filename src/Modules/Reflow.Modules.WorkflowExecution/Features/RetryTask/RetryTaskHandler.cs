using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.RetryTask;

public class RetryTaskHandler(WorkflowExecutionDbContext db, RunEventPublisher events)
{
    public const int MaxTotalAttempts = 5;

    public async Task<Result> Handle(Guid taskRunId, Guid userId, CancellationToken ct)
    {
        var task = await db.TaskRuns.FirstOrDefaultAsync(t => t.Id == taskRunId, ct);
        if (task is null) return Result.Failure("Task not found", 404);

        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == task.WorkflowRunId && r.CreatedBy == userId, ct);
        if (run is null) return Result.Failure("Task not found", 404);

        if (task.Status != TaskRunStatus.Failed)
            return Result.Failure($"Only failed tasks can be retried (current status: {task.Status})", 400);

        var attempts = await db.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct);
        if (attempts >= MaxTotalAttempts)
            return Result.Failure($"Task already attempted {attempts} times (max {MaxTotalAttempts})", 400);

        task.Status = TaskRunStatus.Ready;
        task.Error = null;
        task.CompletedAt = null;
        task.NotBefore = null;

        if (run.Status == WorkflowRunStatus.Failed)
        {
            run.Status = WorkflowRunStatus.Running;
            run.CompletedAt = null;
            run.Error = null;

            var skipped = await db.TaskRuns
                .Where(t => t.WorkflowRunId == run.Id && t.Status == TaskRunStatus.Skipped)
                .ToListAsync(ct);
            foreach (var s in skipped)
            {
                s.Status = TaskRunStatus.Pending;
                s.CompletedAt = null;
            }
        }

        db.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
            Message = $"Task {task.NodeId} queued for manual retry", Level = "Warning"
        });

        await db.SaveChangesAsync(ct);
        await events.RunUpdated(run.Id, userId, ct);
        await events.TaskUpdated(run.Id, task.Id, userId, ct);
        return Result.Success(200);
    }
}
