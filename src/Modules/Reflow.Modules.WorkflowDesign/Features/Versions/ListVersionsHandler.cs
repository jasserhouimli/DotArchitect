using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.Versions;

public class ListVersionsHandler(WorkflowDesignDbContext db)
{
    public async Task<List<WorkflowVersionItem>?> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var owns = await db.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (!owns) return null;

        return await db.WorkflowVersions
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new WorkflowVersionItem(v.Id, v.VersionNumber, v.PublishedAt, v.PublishedBy))
            .ToListAsync(ct);
    }
}

public record WorkflowVersionItem(Guid Id, int VersionNumber, DateTime PublishedAt, Guid PublishedBy);
