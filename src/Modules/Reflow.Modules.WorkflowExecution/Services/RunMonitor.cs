using Reflow.Infrastructure.Runs;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Services;

public class RunMonitor(WorkflowExecutionDbContext db) : IRunMonitor
{
    public Task<int> CountActiveRunsAsync(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        return db.WorkflowRuns.CountAsync(r =>
            r.WorkflowId == workflowId &&
            r.CreatedBy == ownerId &&
            (r.Status == WorkflowRunStatus.Queued || r.Status == WorkflowRunStatus.Running), ct);
    }
}
