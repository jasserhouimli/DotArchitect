using DotArchitect.Modules.Graph.Model;

namespace DotArchitect.Modules.Graph.Algorithms;

public static class GraphTraversal
{
    public static List<string> GetTransitiveDependencies(string nodeId, ProjectGraph graph)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var dep in graph.GetDependencies(current))
            {
                if (!visited.Contains(dep))
                {
                    result.Add(dep);
                    queue.Enqueue(dep);
                }
            }
        }

        return result;
    }

    public static List<string> GetTransitiveDependents(string nodeId, ProjectGraph graph)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var dep in graph.GetDependents(current))
            {
                if (!visited.Contains(dep))
                {
                    result.Add(dep);
                    queue.Enqueue(dep);
                }
            }
        }

        return result;
    }

    public static List<string> GetImpact(string nodeId, ProjectGraph graph)
    {
        return GetTransitiveDependents(nodeId, graph);
    }
}

public static class CycleDetector
{
    public static List<List<string>> DetectCycles(ProjectGraph graph)
    {
        return graph.GetStronglyConnectedComponents();
    }

    public static List<string> GetCyclesInvolving(string nodeId, ProjectGraph graph)
    {
        var sccs = graph.GetStronglyConnectedComponents();
        return sccs
            .Where(scc => scc.Contains(nodeId, StringComparer.OrdinalIgnoreCase))
            .SelectMany(scc => scc)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
