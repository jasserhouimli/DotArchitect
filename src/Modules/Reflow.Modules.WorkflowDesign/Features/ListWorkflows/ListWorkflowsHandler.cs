using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.ListWorkflows;

public class ListWorkflowsHandler(WorkflowDesignDbContext db)
{
    public async Task<List<WorkflowListItem>> Handle(Guid ownerId, CancellationToken ct)
    {
        return await db.Workflows
            .Where(w => w.OwnerId == ownerId)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkflowListItem(w.Id, w.Name, w.Description, w.Status.ToString(), w.CurrentVersion, w.CreatedAt, w.UpdatedAt))
            .ToListAsync(ct);
    }
}

public record WorkflowListItem(Guid Id, string Name, string? Description, string Status, int CurrentVersion, DateTime CreatedAt, DateTime UpdatedAt);
