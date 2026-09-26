using Reflow.Infrastructure.Expressions;
using Reflow.Modules.DataProcessing;
using Reflow.Modules.WorkflowDesign.Domain;

namespace Reflow.Modules.WorkflowDesign.Validation;

public record NodeInput(string NodeId, string NodeType, string? ConfigJson);
public record EdgeInput(string SourceNodeId, string TargetNodeId);
public record WorkflowValidationResult(bool IsValid, string[] Errors, string[] Warnings);

public static class WorkflowValidator
{
    public const int MaxNodes = 100;
    public const int MaxEdges = 300;

    public static WorkflowValidationResult Validate(IReadOnlyList<NodeInput> nodes, IReadOnlyList<EdgeInput> edges)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (nodes.Count == 0)
            errors.Add("Workflow has no nodes");

        if (nodes.Count > MaxNodes)
            errors.Add($"Workflow has {nodes.Count} nodes, maximum is {MaxNodes}");

        if (edges.Count > MaxEdges)
            errors.Add($"Workflow has {edges.Count} edges, maximum is {MaxEdges}");

        var nodeIds = nodes.Select(n => n.NodeId).ToList();

        foreach (var dup in nodeIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"Duplicate node ID: {dup}");

        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                errors.Add("Node with empty ID");
                continue;
            }

            if (!NodeCatalog.SupportedNodeTypes.Contains(node.NodeType))
            {
                errors.Add($"Node '{node.NodeId}' has unsupported type: {node.NodeType}");
                continue;
            }

            if (node.ConfigJson is not null && node.ConfigJson.Length > NodeCatalog.MaxConfigChars)
            {
                errors.Add($"Node '{node.NodeId}' configuration exceeds {NodeCatalog.MaxConfigChars} characters");
                continue;
            }

            errors.AddRange(NodeCatalog.ValidateNodeConfig(node.NodeId, node.NodeType, node.ConfigJson, warnings));
            errors.AddRange(ExpressionValidator.ValidateConfig(node.NodeId, node.ConfigJson));
        }

        var seenEdges = new HashSet<string>();
        foreach (var edge in edges)
        {
            var key = $"{edge.SourceNodeId}->{edge.TargetNodeId}";
            if (!seenEdges.Add(key))
                errors.Add($"Duplicate edge: {edge.SourceNodeId} -> {edge.TargetNodeId}");
            if (!nodeIds.Contains(edge.SourceNodeId))
                errors.Add($"Edge references unknown source node: {edge.SourceNodeId}");
            if (!nodeIds.Contains(edge.TargetNodeId))
                errors.Add($"Edge references unknown target node: {edge.TargetNodeId}");
            if (edge.SourceNodeId == edge.TargetNodeId)
                errors.Add($"Self-referencing edge: {edge.SourceNodeId}");
        }

        if (DetectCycle(nodeIds, edges))
            errors.Add("Workflow graph contains a cycle");

        if (nodes.Count > 0 && edges.Count == 0 && nodes.Count > 1)
            warnings.Add("Nodes are not connected; each node will run independently");

        return new WorkflowValidationResult(errors.Count == 0, errors.ToArray(), warnings.ToArray());
    }

    private static bool DetectCycle(IReadOnlyList<string> nodeIds, IReadOnlyList<EdgeInput> edges)
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
            visited.Add(node);
            stack.Add(node);
            if (adj.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (Dfs(neighbor)) return true;
                    }
                    else if (stack.Contains(neighbor))
                    {
                        return true;
                    }
                }
            }
            stack.Remove(node);
            return false;
        }

        foreach (var id in nodeIds)
        {
            if (!visited.Contains(id) && Dfs(id))
                return true;
        }
        return false;
    }
}
