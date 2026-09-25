using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Realtime;
using Reflow.Modules.WorkflowExecution.Persistence;

namespace Reflow.Modules.WorkflowExecution.Services;

public class RunAccessChecker(WorkflowExecutionDbContext db) : IRunAccessChecker
{
    public async Task<bool> CanAccessRunAsync(Guid runId, Guid userId, CancellationToken ct = default)
    {
        return await db.WorkflowRuns.AnyAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
    }
}
