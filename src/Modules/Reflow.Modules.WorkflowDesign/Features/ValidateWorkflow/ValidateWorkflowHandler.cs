using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Validation;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.ValidateWorkflow;

public class ValidateWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<ValidationResult> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null)
            return new ValidationResult(false, new[] { "Workflow not found" }, Array.Empty<string>());

        var result = WorkflowValidator.Validate(
            workflow.Nodes.Select(n => new NodeInput(n.NodeId, n.NodeType, n.ConfigJson)).ToList(),
            workflow.Edges.Select(e => new EdgeInput(e.SourceNodeId, e.TargetNodeId)).ToList());

        return new ValidationResult(result.IsValid, result.Errors, result.Warnings);
    }
}

public record ValidationResult(bool IsValid, string[] Errors, string[] Warnings);
