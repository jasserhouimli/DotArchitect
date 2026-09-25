using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;

public class GetWorkflowRunHandler(WorkflowExecutionDbContext db)
{
    public async Task<RunDto?> Handle(Guid runId, Guid userId, CancellationToken ct)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
        if (run is null) return null;

        var tasks = await db.TaskRuns
            .AsNoTracking()
            .Where(t => t.WorkflowRunId == runId)
            .Select(t => t.Status)
            .ToListAsync(ct);

        return new RunDto(run.Id, run.WorkflowId, run.VersionNumber, (int)run.Status,
            run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
            tasks.Count,
            tasks.Count(s => s == Domain.TaskRunStatus.Completed),
            tasks.Count(s => s == Domain.TaskRunStatus.Failed),
            run.TriggerKind, run.TriggerName);
    }
}
