using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Features.GetWorkflow;
using Reflow.Modules.WorkflowDesign.Validation;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.UpdateWorkflow;

public class UpdateWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<bool> Handle(Guid workflowId, Guid ownerId, UpdateWorkflowRequest request, CancellationToken ct)
    {
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (workflow is null) return false;

        if (workflow.Status == WorkflowStatus.Archived)
            throw new ArgumentException("Archived workflows cannot be edited");

        if (request.Name is not null) workflow.Name = request.Name;
        if (request.Description is not null) workflow.Description = request.Description;
        if (request.Nodes is not null)
        {
            foreach (var n in request.Nodes)
            {
                if (!WorkflowValidator.SupportedNodeTypes.Contains(n.NodeType))
                    throw new ArgumentException($"Unsupported node type: {n.NodeType}");
                if (n.ConfigJson is not null && n.ConfigJson.Length > WorkflowValidator.MaxConfigChars)
                    throw new ArgumentException($"Node '{n.NodeId}' configuration exceeds {WorkflowValidator.MaxConfigChars} characters");
            }

            var existingNodes = await db.WorkflowNodes.Where(n => n.WorkflowId == workflowId).ToListAsync(ct);
            db.WorkflowNodes.RemoveRange(existingNodes);
            foreach (var n in request.Nodes)
            {
                db.WorkflowNodes.Add(new WorkflowNode
                {
                    Id = Guid.NewGuid(), WorkflowId = workflowId,
                    NodeId = n.NodeId, NodeType = n.NodeType, ConfigJson = n.ConfigJson,
                    Label = n.Label, PositionX = n.PositionX, PositionY = n.PositionY
                });
            }
        }
        if (request.Edges is not null)
        {
            var existingEdges = await db.WorkflowEdges.Where(e => e.WorkflowId == workflowId).ToListAsync(ct);
            db.WorkflowEdges.RemoveRange(existingEdges);
            foreach (var e in request.Edges)
            {
                db.WorkflowEdges.Add(new WorkflowEdge
                {
                    Id = Guid.NewGuid(), WorkflowId = workflowId,
                    SourceNodeId = e.SourceNodeId, TargetNodeId = e.TargetNodeId
                });
            }
        }

        workflow.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public record UpdateWorkflowRequest(string? Name, string? Description, List<NodeDto>? Nodes, List<EdgeDto>? Edges);
