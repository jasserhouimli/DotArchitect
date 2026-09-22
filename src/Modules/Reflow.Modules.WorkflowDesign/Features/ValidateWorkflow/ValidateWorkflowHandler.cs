using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Features.GetWorkflow;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.ValidateWorkflow;

public class ValidateWorkflowHandler(WorkflowDesignDbContext db)
{
    private static readonly HashSet<string> SupportedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http.request", "data.csv.read", "data.validate", "data.filter",
        "data.transform", "data.aggregate", "data.output"
    };

    public async Task<ValidationResult> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null)
            return new ValidationResult(false, new[] { "Workflow not found" }, Array.Empty<string>());

        var errors = new List<string>();
        var warnings = new List<string>();

        if (workflow.Nodes.Count == 0)
            errors.Add("Workflow has no nodes");

        var nodeIds = workflow.Nodes.Select(n => n.NodeId).ToList();
        var duplicateIds = nodeIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var dup in duplicateIds)
            errors.Add($"Duplicate node ID: {dup}");

        foreach (var node in workflow.Nodes)
        {
            if (!SupportedNodeTypes.Contains(node.NodeType))
                warnings.Add($"Node '{node.NodeId}' has unsupported type: {node.NodeType}");
            if (string.IsNullOrEmpty(node.ConfigJson))
                warnings.Add($"Node '{node.NodeId}' has no configuration");
        }

        foreach (var edge in workflow.Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
                errors.Add($"Edge references unknown source node: {edge.SourceNodeId}");
            if (!nodeIds.Contains(edge.TargetNodeId))
                errors.Add($"Edge references unknown target node: {edge.TargetNodeId}");
            if (edge.SourceNodeId == edge.TargetNodeId)
                errors.Add($"Self-referencing edge: {edge.SourceNodeId}");
        }

        var hasCycle = DetectCycle(nodeIds, workflow.Edges);
        if (hasCycle)
            errors.Add("Workflow graph contains a cycle");

        return new ValidationResult(errors.Count == 0, errors.ToArray(), warnings.ToArray());
    }

    private static bool DetectCycle(List<string> nodeIds, List<WorkflowEdge> edges)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var id in nodeIds) adj[id] = new List<string>();
        foreach (var e in edges)
        {
            if (adj.ContainsKey(e.SourceNodeId))
                adj[e.SourceNodeId].Add(e.TargetNodeId);
        }

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        bool Dfs(string node)
        {
            visited.Add(node); stack.Add(node);
            if (adj.TryGetValue(node, out var neighbors))
            {
                foreach (var n in neighbors)
                {
                    if (!visited.Contains(n)) { if (Dfs(n)) return true; }
                    else if (stack.Contains(n)) return true;
                }
            }
            stack.Remove(node);
            return false;
        }

        foreach (var id in nodeIds)
            if (!visited.Contains(id) && Dfs(id)) return true;
        return false;
    }
}

public record ValidationResult(bool IsValid, string[] Errors, string[] Warnings);
