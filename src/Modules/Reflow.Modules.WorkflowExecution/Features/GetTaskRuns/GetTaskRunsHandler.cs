using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;

public class GetTaskRunsHandler(WorkflowExecutionDbContext db)
{
    public async Task<List<TaskRun>> Handle(Guid runId, Guid userId, CancellationToken ct)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
        if (run is null) return new List<TaskRun>();
        return await db.TaskRuns.Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
    }
}
