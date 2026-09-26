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

        var callCheck = CheckWorkflowCalls(workflowId, ownerId, workflow.Nodes);
        if (!callCheck.IsSuccess)
            return Result<int>.Failure(callCheck.Error!, 400);

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

    /// <summary>
    /// Cross-workflow guard for <c>workflow.call</c> nodes: every target must
    /// exist and be published, and publishing must not close a call cycle.
    /// Targets resolve to their latest published versions; the publishing
    /// workflow itself resolves to its draft nodes.
    /// </summary>
    private Result CheckWorkflowCalls(Guid workflowId, Guid ownerId, List<WorkflowNode> draftNodes)
    {
        var direct = WorkflowCallGraph.ExtractTargets(
            draftNodes.Select(n => (n.NodeType, n.ConfigJson)));
        if (direct.Count == 0)
            return Result.Success();

        foreach (var targetId in direct)
        {
            var target = db.Workflows.FirstOrDefault(w => w.Id == targetId && w.OwnerId == ownerId);
            if (target is null)
                return Result.Failure($"Called workflow {targetId} was not found.", 400);
            if (target.Status != WorkflowStatus.Published)
                return Result.Failure($"Called workflow '{target.Name}' is not published.", 400);
        }

        IReadOnlyList<Guid> Outgoing(Guid id)
        {
            if (id == workflowId)
                return direct;
            var definition = db.WorkflowVersions
                .Where(v => v.WorkflowId == id)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.DefinitionJson)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(definition))
                return Array.Empty<Guid>();
            try
            {
                using var doc = JsonDocument.Parse(definition);
                if (!doc.RootElement.TryGetProperty("Nodes", out var nodes)
                    || nodes.ValueKind != JsonValueKind.Array)
                    return Array.Empty<Guid>();
                return WorkflowCallGraph.ExtractTargets(nodes.EnumerateArray().Select(n =>
                    (n.TryGetProperty("NodeType", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? string.Empty : string.Empty,
                     n.TryGetProperty("ConfigJson", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString() : null)));
            }
            catch (JsonException)
            {
                return Array.Empty<Guid>();
            }
        }

        if (WorkflowCallGraph.WouldCycle(workflowId, direct, Outgoing))
            return Result.Failure("Publishing would create a workflow call cycle (a workflow calling itself, directly or through others).", 400);

        return Result.Success();
    }
}
