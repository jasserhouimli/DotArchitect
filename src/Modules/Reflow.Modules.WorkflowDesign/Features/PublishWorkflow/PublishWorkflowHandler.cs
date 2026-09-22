using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Validation;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.PublishWorkflow;

public class PublishWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<Result<int>> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null)
            return Result<int>.Failure("Workflow not found", 404);

        if (workflow.Status == WorkflowStatus.Archived)
            return Result<int>.Failure("Archived workflows cannot be published", 400);

        var validation = WorkflowValidator.Validate(
            workflow.Nodes.Select(n => new NodeInput(n.NodeId, n.NodeType, n.ConfigJson)).ToList(),
            workflow.Edges.Select(e => new EdgeInput(e.SourceNodeId, e.TargetNodeId)).ToList());

        if (!validation.IsValid)
            return Result<int>.Failure("Workflow is invalid: " + string.Join("; ", validation.Errors), 400);

        var versionNumber = workflow.CurrentVersion + 1;
        var definition = JsonSerializer.Serialize(new
        {
            workflow.Name,
            Nodes = workflow.Nodes.Select(n => new { n.NodeId, n.NodeType, n.ConfigJson, n.Label, n.PositionX, n.PositionY }),
            Edges = workflow.Edges.Select(e => new { e.SourceNodeId, e.TargetNodeId })
        });

        var version = new WorkflowVersion
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            VersionNumber = versionNumber,
            DefinitionJson = definition,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = ownerId
        };

        workflow.CurrentVersion = versionNumber;
        workflow.Status = WorkflowStatus.Published;
        workflow.UpdatedAt = DateTime.UtcNow;

        db.WorkflowVersions.Add(version);
        await db.SaveChangesAsync(ct);

        return Result<int>.Success(versionNumber, 200);
    }
}
