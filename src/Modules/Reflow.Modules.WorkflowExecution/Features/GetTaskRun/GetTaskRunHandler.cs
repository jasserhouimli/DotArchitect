using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.GetTaskRun;

public class GetTaskRunHandler(WorkflowExecutionDbContext db)
{
    public async Task<TaskDetailDto?> GetTaskAsync(Guid taskRunId, Guid userId, CancellationToken ct)
    {
        var task = await db.TaskRuns.FirstOrDefaultAsync(t => t.Id == taskRunId, ct);
        if (task is null) return null;

        var owned = await db.WorkflowRuns.AnyAsync(r => r.Id == task.WorkflowRunId && r.CreatedBy == userId, ct);
        if (!owned) return null;

        return new TaskDetailDto(task.Id, task.WorkflowRunId, task.NodeId, task.NodeType,
            (int)task.Status, task.Error, task.CreatedAt, task.StartedAt, task.CompletedAt,
            task.ConfigJson, task.OutputJson);
    }

    public async Task<List<AttemptDto>?> GetAttemptsAsync(Guid taskRunId, Guid userId, CancellationToken ct)
    {
        var task = await db.TaskRuns.FirstOrDefaultAsync(t => t.Id == taskRunId, ct);
        if (task is null) return null;

        var owned = await db.WorkflowRuns.AnyAsync(r => r.Id == task.WorkflowRunId && r.CreatedBy == userId, ct);
        if (!owned) return null;

        return await db.TaskAttempts
            .Where(a => a.TaskRunId == taskRunId)
            .OrderBy(a => a.AttemptNumber)
            .Select(a => new AttemptDto(a.Id, a.AttemptNumber, (int)a.Status, a.StartedAt, a.CompletedAt, a.Error, a.Log))
            .ToListAsync(ct);
    }
}
