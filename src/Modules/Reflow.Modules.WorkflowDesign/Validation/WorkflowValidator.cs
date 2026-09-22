using System.Text.Json;

namespace Reflow.Modules.WorkflowDesign.Validation;

public record NodeInput(string NodeId, string NodeType, string? ConfigJson);
public record EdgeInput(string SourceNodeId, string TargetNodeId);
public record WorkflowValidationResult(bool IsValid, string[] Errors, string[] Warnings);

public static class WorkflowValidator
{
    public const int MaxNodes = 100;
    public const int MaxEdges = 300;
    public const int MaxConfigChars = 20000;
    public const int MaxCsvChars = 1000000;

    public static readonly HashSet<string> SupportedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http.request", "data.csv.read", "data.validate", "data.filter",
        "data.transform", "data.aggregate", "data.output"
    };

    private static readonly HashSet<string> FilterOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "equals", "notEquals", "contains", "notContains",
        "greaterThan", "lessThan", "isEmpty", "isNotEmpty"
    };

    private static readonly HashSet<string> AggregateOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "sum", "avg", "min", "max"
    };

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

            if (!SupportedNodeTypes.Contains(node.NodeType))
            {
                errors.Add($"Node '{node.NodeId}' has unsupported type: {node.NodeType}");
                continue;
            }

            if (node.ConfigJson is not null && node.ConfigJson.Length > MaxConfigChars)
            {
                errors.Add($"Node '{node.NodeId}' configuration exceeds {MaxConfigChars} characters");
                continue;
            }

            errors.AddRange(ValidateNodeConfig(node.NodeId, node.NodeType, node.ConfigJson, warnings));
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

    public static List<string> ValidateNodeConfig(string nodeId, string nodeType, string? configJson, List<string>? warnings = null)
    {
        var errors = new List<string>();
        var configText = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(configText);
        }
        catch (JsonException)
        {
            errors.Add($"Node '{nodeId}' configuration is not valid JSON");
            return errors;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Node '{nodeId}' configuration must be a JSON object");
                return errors;
            }

            var root = doc.RootElement;

            switch (nodeType.ToLowerInvariant())
            {
                case "data.csv.read":
                    if (!TryGetString(root, "csvText", out var csv) || string.IsNullOrWhiteSpace(csv))
                        errors.Add($"Node '{nodeId}' requires 'csvText' with CSV content");
                    else if (csv.Length > MaxCsvChars)
                        errors.Add($"Node '{nodeId}' csvText exceeds {MaxCsvChars} characters");
                    if (root.TryGetProperty("delimiter", out var delim))
                    {
                        if (delim.ValueKind != JsonValueKind.String || delim.GetString() is not { Length: 1 })
                            errors.Add($"Node '{nodeId}' 'delimiter' must be a single character");
                    }
                    if (root.TryGetProperty("hasHeader", out var hasHeader) && hasHeader.ValueKind != JsonValueKind.True && hasHeader.ValueKind != JsonValueKind.False)
                        errors.Add($"Node '{nodeId}' 'hasHeader' must be true or false");
                    break;

                case "http.request":
                    if (!TryGetString(root, "url", out var url) || string.IsNullOrWhiteSpace(url))
                    {
                        errors.Add($"Node '{nodeId}' requires 'url'");
                    }
                    else if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        errors.Add($"Node '{nodeId}' 'url' must be an absolute http(s) URL");
                    }
                    if (root.TryGetProperty("timeoutSeconds", out var timeout))
                    {
                        if (timeout.ValueKind != JsonValueKind.Number || !timeout.TryGetInt32(out var t) || t < 1 || t > 120)
                            errors.Add($"Node '{nodeId}' 'timeoutSeconds' must be between 1 and 120");
                    }
                    break;

                case "data.validate":
                    if (root.TryGetProperty("requiredColumns", out var reqCols))
                    {
                        if (reqCols.ValueKind != JsonValueKind.Array)
                            errors.Add($"Node '{nodeId}' 'requiredColumns' must be an array of strings");
                        else if (reqCols.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                            errors.Add($"Node '{nodeId}' 'requiredColumns' must contain only non-empty strings");
                    }
                    break;

                case "data.filter":
                    if (!TryGetString(root, "column", out var col) || string.IsNullOrWhiteSpace(col))
                        errors.Add($"Node '{nodeId}' requires 'column'");
                    if (!TryGetString(root, "operator", out var op) || !FilterOperators.Contains(op ?? string.Empty))
                        errors.Add($"Node '{nodeId}' 'operator' must be one of: {string.Join(", ", FilterOperators)}");
                    else if (!op!.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)
                        && !op.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase)
                        && !root.TryGetProperty("value", out _))
                        errors.Add($"Node '{nodeId}' requires 'value' for operator '{op}'");
                    break;

                case "data.transform":
                    var hasSelect = root.TryGetProperty("select", out var select) && select.ValueKind == JsonValueKind.Array;
                    var hasRenames = root.TryGetProperty("renames", out var renames) && renames.ValueKind == JsonValueKind.Object;
                    var hasUpper = root.TryGetProperty("upperColumns", out var upper) && upper.ValueKind == JsonValueKind.Array;
                    var hasLower = root.TryGetProperty("lowerColumns", out var lower) && lower.ValueKind == JsonValueKind.Array;
                    if (hasSelect && select.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                        errors.Add($"Node '{nodeId}' 'select' must contain only non-empty strings");
                    if (!hasSelect && !hasRenames && !hasUpper && !hasLower)
                        warnings?.Add($"Node '{nodeId}' has no transform operations (select, renames, upperColumns, lowerColumns)");
                    break;

                case "data.aggregate":
                    if (!root.TryGetProperty("operations", out var ops) || ops.ValueKind != JsonValueKind.Array || ops.GetArrayLength() == 0)
                    {
                        errors.Add($"Node '{nodeId}' requires a non-empty 'operations' array");
                    }
                    else
                    {
                        foreach (var o in ops.EnumerateArray())
                        {
                            if (o.ValueKind != JsonValueKind.Object)
                            {
                                errors.Add($"Node '{nodeId}' has a malformed aggregation operation");
                                continue;
                            }
                            if (!o.TryGetProperty("operation", out var oper) || oper.ValueKind != JsonValueKind.String
                                || !AggregateOperations.Contains(oper.GetString() ?? string.Empty))
                            {
                                errors.Add($"Node '{nodeId}' operation must be one of: {string.Join(", ", AggregateOperations)}");
                                continue;
                            }
                            var operName = oper.GetString()!;
                            if (!operName.Equals("count", StringComparison.OrdinalIgnoreCase)
                                && (!o.TryGetProperty("column", out var acol) || acol.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(acol.GetString())))
                                errors.Add($"Node '{nodeId}' operation '{operName}' requires 'column'");
                        }
                    }
                    if (root.TryGetProperty("groupBy", out var groupBy) && groupBy.ValueKind != JsonValueKind.Array)
                        errors.Add($"Node '{nodeId}' 'groupBy' must be an array of strings");
                    break;

                case "data.output":
                    if (root.TryGetProperty("format", out var format))
                    {
                        var f = format.ValueKind == JsonValueKind.String ? format.GetString() : null;
                        if (!string.Equals(f, "json", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(f, "csv", StringComparison.OrdinalIgnoreCase))
                            errors.Add($"Node '{nodeId}' 'format' must be 'json' or 'csv'");
                    }
                    break;
            }
        }

        return errors;
    }

    public static bool DetectCycle(IReadOnlyList<string> nodeIds, IReadOnlyList<EdgeInput> edges)
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

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString();
        return true;
    }
}
