using Reflow.Infrastructure.Events;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.DeleteWorkflow;

public class DeleteWorkflowHandler(WorkflowDesignDbContext db, IEventBus events)
{
    public async Task<bool> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (workflow is null) return false;

        var nodes = await db.WorkflowNodes.Where(n => n.WorkflowId == workflowId).ToListAsync(ct);
        var edges = await db.WorkflowEdges.Where(e => e.WorkflowId == workflowId).ToListAsync(ct);
        var versions = await db.WorkflowVersions.Where(v => v.WorkflowId == workflowId).ToListAsync(ct);

        db.WorkflowNodes.RemoveRange(nodes);
        db.WorkflowEdges.RemoveRange(edges);
        db.WorkflowVersions.RemoveRange(versions);
        db.Workflows.Remove(workflow);
        await db.SaveChangesAsync(ct);

        try
        {
            // Best-effort: the delete already committed; subscribers clean up their own data.
            await events.PublishAsync(new WorkflowDeletedEvent(workflowId, ownerId), ct);
        }
        catch
        {
        }
        return true;
    }
}
