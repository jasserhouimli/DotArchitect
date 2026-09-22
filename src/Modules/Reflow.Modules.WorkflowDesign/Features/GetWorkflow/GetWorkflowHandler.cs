using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.GetWorkflow;

public class GetWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<WorkflowDetail?> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null) return null;

        return new WorkflowDetail(
            workflow.Id, workflow.Name, workflow.Description, workflow.Status.ToString(),
            workflow.CurrentVersion, workflow.CreatedAt, workflow.UpdatedAt,
            workflow.Nodes.Select(n => new NodeDto(n.NodeId, n.NodeType, n.ConfigJson, n.Label, n.PositionX, n.PositionY)).ToList(),
            workflow.Edges.Select(e => new EdgeDto(e.SourceNodeId, e.TargetNodeId)).ToList()
        );
    }
}

public record WorkflowDetail(Guid Id, string Name, string? Description, string Status, int CurrentVersion, DateTime CreatedAt, DateTime UpdatedAt, List<NodeDto> Nodes, List<EdgeDto> Edges);
public record NodeDto(string NodeId, string NodeType, string? ConfigJson, string? Label, double PositionX, double PositionY);
public record EdgeDto(string SourceNodeId, string TargetNodeId);
