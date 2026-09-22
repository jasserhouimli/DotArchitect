using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.Versions;

public class GetVersionHandler(WorkflowDesignDbContext db)
{
    public async Task<WorkflowVersionDetail?> Handle(Guid workflowId, int versionNumber, Guid ownerId, CancellationToken ct)
    {
        var owns = await db.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (!owns) return null;

        var version = await db.WorkflowVersions
            .FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == versionNumber, ct);

        if (version is null) return null;

        return new WorkflowVersionDetail(version.Id, version.WorkflowId, version.VersionNumber, version.DefinitionJson, version.PublishedAt, version.PublishedBy);
    }
}

public record WorkflowVersionDetail(Guid Id, Guid WorkflowId, int VersionNumber, string DefinitionJson, DateTime PublishedAt, Guid PublishedBy);
