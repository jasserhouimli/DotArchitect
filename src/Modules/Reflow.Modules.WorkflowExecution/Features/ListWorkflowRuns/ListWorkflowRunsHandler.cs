using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.ListWorkflowRuns;

public class ListWorkflowRunsHandler(WorkflowExecutionDbContext db)
{
    public async Task<List<RunDto>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var runs = await db.WorkflowRuns
            .Where(r => r.WorkflowId == workflowId && r.CreatedBy == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var result = new List<RunDto>();
        foreach (var run in runs)
        {
            var statuses = await db.TaskRuns
                .AsNoTracking()
                .Where(t => t.WorkflowRunId == run.Id)
                .Select(t => t.Status)
                .ToListAsync(ct);

            result.Add(new RunDto(run.Id, run.WorkflowId, run.VersionNumber, (int)run.Status,
                run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
                statuses.Count,
                statuses.Count(s => s == Domain.TaskRunStatus.Completed),
                statuses.Count(s => s == Domain.TaskRunStatus.Failed),
                run.TriggerKind, run.TriggerName));
        }

        return result;
    }
}
