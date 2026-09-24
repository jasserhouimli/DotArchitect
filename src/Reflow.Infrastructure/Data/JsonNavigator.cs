using System.Text.Json;

namespace Reflow.Infrastructure.Data;

public static class JsonNavigator
{
    public static bool TryNavigate(JsonElement root, string? path, out JsonElement result, out string error)
    {
        result = root;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return true;

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var index) || index < 0 || index >= current.GetArrayLength())
                {
                    error = $"Path '{path}' does not resolve: array has no index '{segment}'";
                    return false;
                }
                current = current[index];
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    error = $"Path '{path}' does not resolve: object has no property '{segment}'";
                    return false;
                }
            }
            else
            {
                error = $"Path '{path}' does not resolve: cannot descend into '{segment}'";
                return false;
            }
        }

        result = current;
        return true;
    }
}
