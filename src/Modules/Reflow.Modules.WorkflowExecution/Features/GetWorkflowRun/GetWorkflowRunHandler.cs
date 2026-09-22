using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;

public class GetWorkflowRunHandler(WorkflowExecutionDbContext db)
{
    public async Task<WorkflowRun?> Handle(Guid runId, Guid userId, CancellationToken ct)
    {
        return await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
    }
}
