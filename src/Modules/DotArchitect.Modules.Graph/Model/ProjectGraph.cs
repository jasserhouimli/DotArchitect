namespace DotArchitect.Modules.Graph.Model;

public class GraphNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public string? TargetFrameworks { get; set; }
}

public class GraphEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public class ProjectGraph
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _adjacencyList = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _reverseAdjacencyList = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, GraphNode> Nodes => _nodes;
    public IReadOnlyCollection<GraphEdge> Edges => _adjacencyList
        .SelectMany(kvp => kvp.Value.Select(target => new GraphEdge { Source = kvp.Key, Target = target }))
        .ToList();

    public void AddNode(GraphNode node)
    {
        _nodes[node.Id] = node;
        _adjacencyList.TryAdd(node.Id, new List<string>());
        _reverseAdjacencyList.TryAdd(node.Id, new List<string>());
    }

    public void AddEdge(string sourceId, string targetId)
    {
        if (!_adjacencyList.ContainsKey(sourceId))
            _adjacencyList[sourceId] = new List<string>();
        if (!_reverseAdjacencyList.ContainsKey(targetId))
            _reverseAdjacencyList[targetId] = new List<string>();

        if (!_adjacencyList[sourceId].Contains(targetId, StringComparer.OrdinalIgnoreCase))
        {
            _adjacencyList[sourceId].Add(targetId);
            _reverseAdjacencyList[targetId].Add(sourceId);
        }
    }

    public List<string> GetDependencies(string nodeId)
    {
        return _adjacencyList.TryGetValue(nodeId, out var deps)
            ? deps
            : new List<string>();
    }

    public List<string> GetDependents(string nodeId)
    {
        return _reverseAdjacencyList.TryGetValue(nodeId, out var dependents)
            ? dependents
            : new List<string>();
    }

    public List<List<string>> GetStronglyConnectedComponents()
    {
        var index = 0;
        var stack = new Stack<string>();
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowlinks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sccs = new List<List<string>>();

        void StrongConnect(string v)
        {
            indices[v] = index;
            lowlinks[v] = index;
            index++;
            stack.Push(v);
            onStack.Add(v);

            if (_adjacencyList.TryGetValue(v, out var neighbors))
            {
                foreach (var w in neighbors)
                {
                    if (!indices.ContainsKey(w))
                    {
                        StrongConnect(w);
                        lowlinks[v] = Math.Min(lowlinks[v], lowlinks[w]);
                    }
                    else if (onStack.Contains(w))
                    {
                        lowlinks[v] = Math.Min(lowlinks[v], indices[w]);
                    }
                }
            }

            if (lowlinks[v] == indices[v])
            {
                var scc = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    scc.Add(w);
                } while (w != v);

                if (scc.Count > 1 || HasSelfLoop(v))
                    sccs.Add(scc);
            }
        }

        foreach (var nodeId in _nodes.Keys)
        {
            if (!indices.ContainsKey(nodeId))
                StrongConnect(nodeId);
        }

        return sccs;
    }

    private bool HasSelfLoop(string nodeId)
    {
        return _adjacencyList.TryGetValue(nodeId, out var neighbors)
            && neighbors.Contains(nodeId, StringComparer.OrdinalIgnoreCase);
    }
}
