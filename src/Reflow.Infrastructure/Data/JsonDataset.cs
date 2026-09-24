using System.Text.Json;

namespace Reflow.Infrastructure.Data;

public static class JsonDataset
{
    public static Dataset FromJsonText(string body, string? rootPath, string nodeId)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Node '{nodeId}' did not receive valid JSON");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!JsonNavigator.TryNavigate(root, rootPath, out var target, out var error))
                throw new InvalidOperationException($"Node '{nodeId}': {error}");

            return FromElement(target, nodeId);
        }
    }

    public static Dataset FromElement(JsonElement el, string nodeId)
    {
        if (el.ValueKind == JsonValueKind.Object)
            return SingleRow(el);
        if (el.ValueKind == JsonValueKind.Array)
        {
            var items = el.EnumerateArray().ToList();
            if (items.Count > Dataset.MaxRows)
                throw new InvalidOperationException($"Node '{nodeId}' received too many records (max {Dataset.MaxRows})");
            return ArrayRows(items);
        }
        throw new InvalidOperationException($"Node '{nodeId}' expected a JSON object or array");
    }

    private static Dataset SingleRow(JsonElement obj)
    {
        var dataset = new Dataset();
        var row = new List<string?>();
        foreach (var p in obj.EnumerateObject())
        {
            dataset.Columns.Add(p.Name);
            row.Add(Scalar(p.Value));
        }
        dataset.Rows.Add(row);
        dataset.Quality.InputCount = 1;
        dataset.Quality.OutputCount = 1;
        return dataset;
    }

    private static Dataset ArrayRows(List<JsonElement> items)
    {
        var dataset = new Dataset();
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in item.EnumerateObject())
                {
                    if (!columnIndex.ContainsKey(p.Name))
                    {
                        columnIndex[p.Name] = dataset.Columns.Count;
                        dataset.Columns.Add(p.Name);
                    }
                }
            }
            else if (!columnIndex.ContainsKey("value"))
            {
                columnIndex["value"] = dataset.Columns.Count;
                dataset.Columns.Add("value");
            }
        }

        foreach (var item in items)
        {
            var row = Enumerable.Repeat<string?>(null, dataset.Columns.Count).ToList();
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in item.EnumerateObject())
                    row[columnIndex[p.Name]] = Scalar(p.Value);
            }
            else
            {
                row[columnIndex["value"]] = Scalar(item);
            }
            dataset.Rows.Add(row);
        }

        dataset.Quality.InputCount = dataset.Rows.Count;
        dataset.Quality.OutputCount = dataset.Rows.Count;
        return dataset;
    }

    private static string? Scalar(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }
}
