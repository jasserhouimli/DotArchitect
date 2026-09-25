using System.Text.Json;
using Reflow.Infrastructure.Events;
using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Snapshots;
using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Validation;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.Snapshots;

public class WorkflowSnapshotProvider(WorkflowDesignDbContext db) : IWorkflowSnapshotProvider
{
    public async Task<Result<WorkflowSnapshot>> GetPublishedSnapshotAsync(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (workflow is null)
            return Result<WorkflowSnapshot>.Failure("Workflow not found", 404);
        if (workflow.Status != WorkflowStatus.Published)
            return Result<WorkflowSnapshot>.Failure("Workflow must be published before running", 400);

        var version = await db.WorkflowVersions
            .FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == workflow.CurrentVersion, ct);
        if (version is null)
            return Result<WorkflowSnapshot>.Failure("Published version not found", 404);

        if (!TryParseSnapshot(version.DefinitionJson, out var nodes, out var edges))
            return Result<WorkflowSnapshot>.Failure("Published version is corrupted", 500);

        var validation = WorkflowValidator.Validate(nodes, edges);
        if (!validation.IsValid)
            return Result<WorkflowSnapshot>.Failure("Published version is invalid: " + string.Join("; ", validation.Errors), 400);

        return Result<WorkflowSnapshot>.Success(new WorkflowSnapshot(
            workflowId,
            workflow.Name,
            version.VersionNumber,
            version.Id,
            nodes.Select(n => new SnapshotNode(n.NodeId, n.NodeType, n.ConfigJson)).ToList(),
            edges.Select(e => new SnapshotEdge(e.SourceNodeId, e.TargetNodeId)).ToList()));
    }

    private static bool TryParseSnapshot(string definitionJson, out List<NodeInput> nodes, out List<EdgeInput> edges)
    {
        nodes = new List<NodeInput>();
        edges = new List<EdgeInput>();
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Nodes", out var nodesEl) || nodesEl.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var n in nodesEl.EnumerateArray())
            {
                var nodeId = n.TryGetProperty("NodeId", out var id) ? id.GetString() : null;
                var nodeType = n.TryGetProperty("NodeType", out var type) ? type.GetString() : null;
                if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(nodeType))
                    return false;
                string? config = null;
                if (n.TryGetProperty("ConfigJson", out var cfg) && cfg.ValueKind == JsonValueKind.String)
                    config = cfg.GetString();
                nodes.Add(new NodeInput(nodeId, nodeType, config));
            }

            if (root.TryGetProperty("Edges", out var edgesEl) && edgesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in edgesEl.EnumerateArray())
                {
                    var source = e.TryGetProperty("SourceNodeId", out var s) ? s.GetString() : null;
                    var target = e.TryGetProperty("TargetNodeId", out var t) ? t.GetString() : null;
                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                        return false;
                    edges.Add(new EdgeInput(source, target));
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
