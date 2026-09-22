using Reflow.Modules.WorkflowExecution.Data;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;

public class GetTaskRunsHandler(WorkflowExecutionDbContext db)
{
    public async Task<List<TaskDto>?> Handle(Guid runId, Guid userId, CancellationToken ct)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
        if (run is null) return null;

        var tasks = await db.TaskRuns
            .AsNoTracking()
            .Where(t => t.WorkflowRunId == runId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var result = new List<TaskDto>();
        foreach (var task in tasks)
        {
            var attemptCount = await db.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct);

            string? summary = null;
            int? rowCount = null;
            if (task.OutputJson is not null && Dataset.TryFromJson(task.OutputJson) is { } dataset)
            {
                summary = dataset.Summary();
                rowCount = dataset.Rows.Count;
            }

            result.Add(new TaskDto(task.Id, task.WorkflowRunId, task.NodeId, task.NodeType,
                (int)task.Status, task.Error, task.CreatedAt, task.StartedAt, task.CompletedAt,
                attemptCount, summary, rowCount));
        }

        return result;
    }
}
