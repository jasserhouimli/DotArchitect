using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.ArchiveWorkflow;

public class ArchiveWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<Result> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null)
            return Result.Failure("Workflow not found", 404);

        if (workflow.Status == WorkflowStatus.Archived)
            return Result.Failure("Workflow is already archived", 400);

        workflow.Status = WorkflowStatus.Archived;
        workflow.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(200);
    }
}
