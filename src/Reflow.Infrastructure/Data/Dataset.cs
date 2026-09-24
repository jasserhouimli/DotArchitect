using System.Text.Json;

namespace Reflow.Infrastructure.Data;

public sealed class DatasetQuality
{
    public int InputCount { get; set; }
    public int OutputCount { get; set; }
    public int RejectedCount { get; set; }
    public int DuplicateCount { get; set; }
    public Dictionary<string, int> FailuresByRule { get; set; } = new();
}

public sealed class Dataset
{
    public const int MaxRows = 50000;
    public const int MaxStoredChars = 500000;

    public List<string> Columns { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
    public DatasetQuality Quality { get; set; } = new();

    public static Dataset Empty() => new();

    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            columns = Columns,
            rows = Rows,
            quality = new
            {
                inputCount = Quality.InputCount,
                outputCount = Quality.OutputCount,
                rejectedCount = Quality.RejectedCount,
                duplicateCount = Quality.DuplicateCount,
                failuresByRule = Quality.FailuresByRule
            }
        });
    }

    public static Dataset? TryFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (!root.TryGetProperty("columns", out _) && !root.TryGetProperty("rows", out _))
                return null;

            var dataset = new Dataset();

            if (root.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cols.EnumerateArray())
                    dataset.Columns.Add(c.GetString() ?? string.Empty);
            }

            if (root.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rows.EnumerateArray())
                {
                    var row = new List<string?>();
                    foreach (var cell in r.EnumerateArray())
                        row.Add(cell.ValueKind == JsonValueKind.Null ? null : cell.GetString());
                    dataset.Rows.Add(row);
                }
            }

            if (root.TryGetProperty("quality", out var q) && q.ValueKind == JsonValueKind.Object)
            {
                dataset.Quality = new DatasetQuality
                {
                    InputCount = GetInt(q, "inputCount"),
                    OutputCount = GetInt(q, "outputCount"),
                    RejectedCount = GetInt(q, "rejectedCount"),
                    DuplicateCount = GetInt(q, "duplicateCount")
                };
                if (q.TryGetProperty("failuresByRule", out var fbr) && fbr.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in fbr.EnumerateObject())
                        dataset.Quality.FailuresByRule[p.Name] = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var n) ? n : 0;
                }
            }

            return dataset;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Dataset Merge(IEnumerable<Dataset> inputs)
    {
        var merged = new Dataset();
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            foreach (var col in input.Columns)
            {
                if (!columnIndex.ContainsKey(col))
                {
                    columnIndex[col] = merged.Columns.Count;
                    merged.Columns.Add(col);
                }
            }
        }

        foreach (var input in inputs)
        {
            var map = input.Columns.Select(c => columnIndex[c]).ToArray();
            foreach (var row in input.Rows)
            {
                var mergedRow = Enumerable.Repeat<string?>(null, merged.Columns.Count).ToList();
                for (var i = 0; i < row.Count && i < map.Length; i++)
                    mergedRow[map[i]] = row[i];
                merged.Rows.Add(mergedRow);
                if (merged.Rows.Count >= MaxRows) break;
            }
            if (merged.Rows.Count >= MaxRows) break;
        }

        merged.Quality.InputCount = merged.Rows.Count;
        merged.Quality.OutputCount = merged.Rows.Count;
        return merged;
    }

    public int ColumnIndex(string name)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public string Summary()
    {
        return $"{Quality.OutputCount} rows x {Columns.Count} cols " +
            $"(in={Quality.InputCount}, rejected={Quality.RejectedCount}, dupes={Quality.DuplicateCount})";
    }

    private static int GetInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : 0;
    }
}
