using System.Text.Json;

namespace Reflow.Modules.WorkflowDesign.Validation;

/// <summary>
/// Cross-workflow call analysis for the <c>workflow.call</c> node type:
/// extracting call targets from configs and detecting call cycles at publish
/// time (A calls B calls A would loop forever at runtime).
/// </summary>
public static class WorkflowCallGraph
{
    // Bound on traversal depth. Cycles deeper than this are still contained at
    // runtime: every nesting level is bounded by the call node's own timeout.
    public const int MaxDepth = 25;

    /// <summary>Target workflow ids referenced by draft nodes.</summary>
    public static List<Guid> ExtractTargets(IEnumerable<(string NodeType, string? ConfigJson)> nodes)
    {
        var targets = new List<Guid>();
        foreach (var (nodeType, configJson) in nodes)
        {
            if (!string.Equals(nodeType, "workflow.call", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(configJson))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.TryGetProperty("targetWorkflowId", out var target)
                    && target.ValueKind == JsonValueKind.String
                    && Guid.TryParse(target.GetString(), out var id)
                    && !targets.Contains(id))
                    targets.Add(id);
            }
            catch (JsonException)
            {
                // Malformed configs are reported by the per-type validator.
            }
        }
        return targets;
    }

    /// <summary>
    /// True when publishing <paramref name="publishingId"/> with the given
    /// direct targets would create a call cycle. <paramref name="outgoing"/>
    /// returns the call targets of a workflow's latest <i>published</i>
    /// version (the publishing workflow itself resolves to its draft nodes).
    /// </summary>
    public static bool WouldCycle(
        Guid publishingId,
        IReadOnlyList<Guid> directTargets,
        Func<Guid, IReadOnlyList<Guid>> outgoing,
        int maxDepth = MaxDepth)
    {
        var visited = new HashSet<Guid> { publishingId };
        var frontier = new Queue<(Guid id, int depth)>(directTargets.Select(t => (t, 1)));

        while (frontier.Count > 0)
        {
            var (id, depth) = frontier.Dequeue();
            if (id == publishingId)
                return true;
            if (depth >= maxDepth || !visited.Add(id))
                continue;
            foreach (var next in outgoing(id))
                frontier.Enqueue((next, depth + 1));
        }

        return false;
    }
}
