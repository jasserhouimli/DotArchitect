using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.ListWorkflowRuns;

public class ListWorkflowRunsHandler(WorkflowExecutionDbContext db)
{
    public async Task<List<WorkflowRun>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        return await db.WorkflowRuns
            .Where(r => r.WorkflowId == workflowId && r.CreatedBy == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}
