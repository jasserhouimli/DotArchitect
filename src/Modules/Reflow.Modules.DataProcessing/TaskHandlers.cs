using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Reflow.Infrastructure.Data;
using Reflow.Infrastructure.Http;
using Reflow.Infrastructure.Storage;

namespace Reflow.Modules.DataProcessing;

public record TaskExecutionContext(
    Guid WorkflowRunId,
    Guid TaskRunId,
    string NodeId,
    string NodeType,
    string? ConfigJson,
    IReadOnlyList<Dataset> Inputs,
    Guid WorkflowId,
    string? TriggerPayload);

public record TaskExecutionResult(
    bool IsSuccess,
    string? Error,
    string? OutputJson,
    string? Log,
    bool IsRetryable = false,
    /// <summary>
    /// Set when the task suspends instead of finishing (e.g. workflow.call
    /// waiting on a child run). The worker parks the task as Waiting; it is
    /// re-executed once woken, never retried as a failure.
    /// </summary>
    Guid? WaitForChildRunId = null);

public interface ITaskHandler
{
    string TaskType { get; }
    bool SupportsRetry { get; }
    Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken ct);
}

public class TaskHandlerRegistry
{
    private readonly Dictionary<string, ITaskHandler> _handlers;
    public TaskHandlerRegistry(IEnumerable<ITaskHandler> handlers) => _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);
    public bool TryGet(string taskType, out ITaskHandler? handler) => _handlers.TryGetValue(taskType, out handler);
}

internal static class HandlerHelpers
{
    public static JsonElement ParseConfig(string? configJson, string nodeId)
    {
        var text = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Node '{nodeId}' configuration is not valid JSON");
        }
    }

    public static string GetString(JsonElement root, string name, string? fallback = null)
    {
        if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString() ?? fallback ?? string.Empty;
        return fallback ?? string.Empty;
    }

    public static List<string> GetStringList(JsonElement root, string name)
    {
        var result = new List<string>();
        if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(arr.EnumerateArray()
                .Where(c => c.ValueKind == JsonValueKind.String)
                .Select(c => c.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        return result;
    }

    public static Dataset RequireInput(TaskExecutionContext ctx)
    {
        if (ctx.Inputs.Count == 0)
            throw new InvalidOperationException($"Node '{ctx.NodeId}' has no input data; connect a source node (CSV read, JSON read or HTTP request)");
        return Dataset.Merge(ctx.Inputs);
    }

    public static async Task<string> RequireUploadText(TaskExecutionContext ctx, IUploadStore uploads, string nodeId, JsonElement config, CancellationToken ct)
    {
        var source = GetString(config, "source", "text");
        if (source.Equals("upload", StringComparison.OrdinalIgnoreCase))
        {
            var fileId = GetString(config, "fileId");
            if (!LocalUploadStore.IsValidFileId(fileId))
                throw new InvalidOperationException($"Node '{nodeId}' has an invalid 'fileId'");
            var text = await uploads.ReadTextAsync(ctx.WorkflowId, fileId, ct);
            if (text is null)
                throw new InvalidOperationException($"Node '{nodeId}' references a file that no longer exists");
            return text;
        }
        return string.Empty;
    }

    public static void Deduplicate(Dataset dataset, List<string> columns, string nodeId)
    {
        var indexes = columns.Select(c => dataset.ColumnIndex(c)).ToList();
        if (indexes.Any(i => i < 0))
            throw new InvalidOperationException($"Node '{nodeId}' dedupe references an unknown column");

        var seen = new HashSet<string>();
        var kept = new List<List<string?>>();
        foreach (var row in dataset.Rows)
        {
            var key = string.Join("\u001f", indexes.Select(i => i < row.Count ? row[i] ?? string.Empty : string.Empty));
            if (seen.Add(key))
                kept.Add(row);
        }
        dataset.Quality.DuplicateCount = dataset.Rows.Count - kept.Count;
        dataset.Quality.RejectedCount += dataset.Quality.DuplicateCount;
        dataset.Rows.Clear();
        dataset.Rows.AddRange(kept);
    }

    public static TaskExecutionResult Ok(Dataset dataset, string log)
    {
        dataset.Quality.OutputCount = dataset.Rows.Count;
        var json = dataset.ToJson();
        if (json.Length > Dataset.MaxStoredChars)
            return new TaskExecutionResult(false, $"Result too large to store ({json.Length} chars, max {Dataset.MaxStoredChars})", null, null);
        return new TaskExecutionResult(true, null, json, $"{log} [{dataset.Summary()}]");
    }

    public static TaskExecutionResult Fail(string error, bool retryable = false)
        => new(false, error, null, null, retryable);
}

public class CsvReadHandler : ITaskHandler
{
    private readonly IUploadStore _uploads;

    public CsvReadHandler(IUploadStore uploads) => _uploads = uploads;

    public string TaskType => "data.csv.read";
    public bool SupportsRetry => false;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            var source = HandlerHelpers.GetString(config, "source", "text");

            string csvText;
            if (source.Equals("upload", StringComparison.OrdinalIgnoreCase))
                csvText = await HandlerHelpers.RequireUploadText(ctx, _uploads, ctx.NodeId, config, ct);
            else
                csvText = HandlerHelpers.GetString(config, "csvText");

            if (string.IsNullOrWhiteSpace(csvText))
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'csvText' with CSV content");

            var delimiterText = HandlerHelpers.GetString(config, "delimiter", ",");
            if (delimiterText.Length != 1)
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' 'delimiter' must be a single character");

            var hasHeader = true;
            if (config.TryGetProperty("hasHeader", out var hh) && hh.ValueKind == JsonValueKind.False)
                hasHeader = false;

            var trim = true;
            if (config.TryGetProperty("trim", out var tr) && tr.ValueKind == JsonValueKind.False)
                trim = false;

            var dataset = CsvParser.Parse(csvText, delimiterText[0], hasHeader);

            if (trim)
            {
                foreach (var row in dataset.Rows)
                {
                    for (var i = 0; i < row.Count; i++)
                    {
                        if (row[i] is not null)
                            row[i] = row[i]!.Trim();
                    }
                }
            }

            var nullValues = new HashSet<string> { string.Empty };
            if (config.TryGetProperty("nullValues", out var nv) && nv.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in nv.EnumerateArray())
                {
                    if (v.ValueKind == JsonValueKind.String && v.GetString() is { } s)
                        nullValues.Add(s);
                }
            }
            foreach (var row in dataset.Rows)
            {
                for (var i = 0; i < row.Count; i++)
                {
                    if (row[i] is not null && nullValues.Contains(row[i]!))
                        row[i] = null;
                }
            }

            if (config.TryGetProperty("skipRows", out var sk) && sk.TryGetInt32(out var skip) && skip > 0)
                dataset.Rows.RemoveRange(0, Math.Min(skip, dataset.Rows.Count));

            if (config.TryGetProperty("maxRows", out var mr) && mr.TryGetInt32(out var maxRows) && dataset.Rows.Count > maxRows)
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' CSV has {dataset.Rows.Count} rows, over the configured max of {maxRows}");

            dataset.Quality.InputCount = dataset.Rows.Count;
            dataset.Quality.OutputCount = dataset.Rows.Count;

            if (config.TryGetProperty("dedupeColumns", out var dedupe) && dedupe.ValueKind == JsonValueKind.Array)
            {
                var cols = dedupe.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (cols.Count > 0)
                    HandlerHelpers.Deduplicate(dataset, cols, ctx.NodeId);
            }

            return HandlerHelpers.Ok(dataset, $"CSV parsed: {dataset.Rows.Count} rows");
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }
    }
}

public class JsonReadHandler : ITaskHandler
{
    private readonly IUploadStore _uploads;

    public JsonReadHandler(IUploadStore uploads) => _uploads = uploads;

    public string TaskType => "data.json.read";
    public bool SupportsRetry => false;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            var source = HandlerHelpers.GetString(config, "source", "text");

            if (source.Equals("input", StringComparison.OrdinalIgnoreCase))
                return ExecuteFromColumn(ctx, config);

            string jsonText;
            if (source.Equals("upload", StringComparison.OrdinalIgnoreCase))
                jsonText = await HandlerHelpers.RequireUploadText(ctx, _uploads, ctx.NodeId, config, ct);
            else
                jsonText = HandlerHelpers.GetString(config, "jsonText");

            if (string.IsNullOrWhiteSpace(jsonText))
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'jsonText' with JSON content");

            var rootPath = HandlerHelpers.GetString(config, "rootPath");
            var dataset = JsonDataset.FromJsonText(jsonText,
                string.IsNullOrWhiteSpace(rootPath) ? null : rootPath, ctx.NodeId);

            return HandlerHelpers.Ok(dataset, $"JSON parsed: {dataset.Rows.Count} records");
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }
    }

    private static TaskExecutionResult ExecuteFromColumn(TaskExecutionContext ctx, JsonElement config)
    {
        var input = HandlerHelpers.RequireInput(ctx);
        var column = HandlerHelpers.GetString(config, "column");
        if (string.IsNullOrWhiteSpace(column))
            return HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'column' when source is 'input'");
        var colIdx = input.ColumnIndex(column);
        if (colIdx < 0)
            return HandlerHelpers.Fail($"Column '{column}' does not exist in input data");

        var rootPath = HandlerHelpers.GetString(config, "rootPath");
        if (string.IsNullOrWhiteSpace(rootPath))
            rootPath = null;

        var outColumns = new List<string>(input.Columns);
        var records = new List<Dictionary<string, string?>>();
        var rejected = 0;
        var failures = new Dictionary<string, int>();

        void Reject(string rule)
        {
            rejected++;
            failures[rule] = failures.TryGetValue(rule, out var n) ? n + 1 : 1;
        }

        foreach (var row in input.Rows)
        {
            var cell = colIdx < row.Count ? row[colIdx] : null;
            if (string.IsNullOrWhiteSpace(cell))
            {
                Reject("json:empty");
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(cell);
            }
            catch (JsonException)
            {
                Reject("json:parse");
                continue;
            }

            using (doc)
            {
                var target = doc.RootElement;
                if (rootPath is not null)
                {
                    if (!JsonNavigator.TryNavigate(target, rootPath, out var navigated, out _))
                    {
                        Reject("json:parse");
                        continue;
                    }
                    target = navigated.Clone();
                }

                if (target.ValueKind == JsonValueKind.Array)
                {
                    var items = target.EnumerateArray().ToList();
                    if (items.Count == 0)
                    {
                        Reject("json:empty");
                        continue;
                    }
                    if (records.Count + items.Count > Dataset.MaxRows)
                        return HandlerHelpers.Fail($"Node '{ctx.NodeId}' exploded to too many rows (max {Dataset.MaxRows})");
                    foreach (var item in items)
                        records.Add(MergeRecord(row, input.Columns, item, outColumns));
                }
                else if (target.ValueKind == JsonValueKind.Object)
                {
                    records.Add(MergeRecord(row, input.Columns, target, outColumns));
                }
                else
                {
                    // Bare scalar: nothing to unpack, row passes through unchanged.
                    records.Add(BaseRecord(row, input.Columns));
                }
            }
        }

        var dataset = new Dataset();
        dataset.Columns.AddRange(outColumns);
        foreach (var record in records)
        {
            var outRow = new List<string?>();
            foreach (var col in outColumns)
                outRow.Add(record.TryGetValue(col, out var v) ? v : null);
            dataset.Rows.Add(outRow);
        }
        dataset.Quality.InputCount = input.Rows.Count;
        dataset.Quality.RejectedCount = rejected;
        dataset.Quality.FailuresByRule = failures;

        return HandlerHelpers.Ok(dataset,
            $"Unpacked JSON from '{column}': {dataset.Rows.Count} rows, {rejected} rejected");
    }

    private static Dictionary<string, string?> BaseRecord(List<string?> row, List<string> columns)
    {
        var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
            record[columns[i]] = i < row.Count ? row[i] : null;
        return record;
    }

    private static Dictionary<string, string?> MergeRecord(
        List<string?> row, List<string> columns, JsonElement element, List<string> outColumns)
    {
        var record = BaseRecord(row, columns);
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in element.EnumerateObject())
            {
                record[p.Name] = ScalarValue(p.Value);
                if (!outColumns.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    outColumns.Add(p.Name);
            }
        }
        else
        {
            record["value"] = ScalarValue(element);
            if (!outColumns.Contains("value", StringComparer.OrdinalIgnoreCase))
                outColumns.Add("value");
        }
        return record;
    }

    private static string? ScalarValue(JsonElement el)
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

public class ValidateHandler : ITaskHandler
{
    public string TaskType => "data.validate";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var required = HandlerHelpers.GetStringList(config, "requiredColumns");
            foreach (var col in required)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Required column '{col}' does not exist in input data"));
            }

            var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config.TryGetProperty("columnTypes", out var ct2) && ct2.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in ct2.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                        types[p.Name] = p.Value.GetString()!;
                }
                foreach (var col in types.Keys)
                {
                    if (input.ColumnIndex(col) < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
                }
            }

            var unique = HandlerHelpers.GetStringList(config, "uniqueColumns");
            foreach (var col in unique)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
            }
            var seenUnique = unique.ToDictionary(c => c, _ => new HashSet<string>(), StringComparer.OrdinalIgnoreCase);

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);

            foreach (var row in input.Rows)
            {
                var failures = new List<string>();
                foreach (var col in required)
                {
                    var idx = input.ColumnIndex(col);
                    var value = idx < row.Count ? row[idx] : null;
                    if (string.IsNullOrWhiteSpace(value))
                        failures.Add($"required:{col}");
                }
                foreach (var (col, type) in types)
                {
                    var idx = input.ColumnIndex(col);
                    var value = idx < row.Count ? row[idx] : null;
                    if (!CheckType(value, type))
                        failures.Add($"type:{col}");
                }
                foreach (var col in unique)
                {
                    var idx = input.ColumnIndex(col);
                    var value = idx < row.Count ? row[idx] ?? string.Empty : string.Empty;
                    if (!seenUnique[col].Add(value))
                        failures.Add($"unique:{col}");
                }

                if (failures.Count == 0)
                {
                    dataset.Rows.Add(row);
                }
                else
                {
                    dataset.Quality.RejectedCount++;
                    foreach (var rule in failures.Distinct())
                        dataset.Quality.FailuresByRule[rule] = dataset.Quality.FailuresByRule.TryGetValue(rule, out var n) ? n + 1 : 1;
                }
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Validated {input.Rows.Count} rows: {dataset.Rows.Count} valid, {dataset.Quality.RejectedCount} rejected"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static bool CheckType(string? value, string type)
    {
        if (value is null)
            return false;
        return type.ToLowerInvariant() switch
        {
            "string" => true,
            "number" => double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _),
            "integer" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "boolean" => value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase),
            "date" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            _ => true
        };
    }
}

public class FilterHandler : ITaskHandler
{
    public string TaskType => "data.filter";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var column = HandlerHelpers.GetString(config, "column");
            var op = HandlerHelpers.GetString(config, "operator");
            if (string.IsNullOrWhiteSpace(column))
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'column'"));
            if (string.IsNullOrWhiteSpace(op))
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'operator'"));

            var colIdx = input.ColumnIndex(column);
            if (colIdx < 0)
                return Task.FromResult(HandlerHelpers.Fail($"Column '{column}' does not exist in input data"));

            var needsValue = !op.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)
                && !op.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase);
            string? value = null;
            List<string>? values = null;
            if (needsValue)
            {
                if (!config.TryGetProperty("value", out var vp))
                    return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'value' for operator '{op}'"));
                if (op.Equals("inList", StringComparison.OrdinalIgnoreCase))
                {
                    if (vp.ValueKind != JsonValueKind.Array)
                        return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'value' to be an array for operator 'inList'"));
                    values = vp.EnumerateArray()
                        .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.GetRawText())
                        .ToList();
                }
                else
                {
                    value = vp.ValueKind == JsonValueKind.Null ? null : vp.ValueKind == JsonValueKind.String ? vp.GetString() : vp.GetRawText();
                }
            }

            Regex? regex = null;
            if (op.Equals("matches", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    regex = new Regex(value ?? string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException ex)
                {
                    return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' has an invalid pattern: {ex.Message}"));
                }
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);

            foreach (var row in input.Rows)
            {
                var cell = colIdx < row.Count ? row[colIdx] : null;
                bool keep;
                try
                {
                    keep = Matches(cell, op, value, values, regex);
                }
                catch (RegexMatchTimeoutException)
                {
                    return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' pattern matching timed out"));
                }
                if (keep)
                    dataset.Rows.Add(row);
                else
                    dataset.Quality.RejectedCount++;
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Filter {column} {op}: {dataset.Rows.Count} kept, {dataset.Quality.RejectedCount} removed"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static bool Matches(string? cell, string op, string? value, List<string>? values, Regex? regex)
    {
        return op.ToLowerInvariant() switch
        {
            "equals" => string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "contains" => (cell ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "notcontains" => !(cell ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "startswith" => (cell ?? string.Empty).StartsWith(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "endswith" => (cell ?? string.Empty).EndsWith(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "matches" => regex!.IsMatch(cell ?? string.Empty),
            "inlist" => values!.Any(v => string.Equals(cell ?? string.Empty, v, StringComparison.OrdinalIgnoreCase)),
            "isempty" => string.IsNullOrWhiteSpace(cell),
            "isnotempty" => !string.IsNullOrWhiteSpace(cell),
            "greaterthan" => CompareNumeric(cell, value) > 0,
            "lessthan" => CompareNumeric(cell, value) < 0,
            _ => false
        };
    }

    private static int CompareNumeric(string? cell, string? value)
    {
        if (double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
            return a.CompareTo(b);
        return -1;
    }
}

public class TransformHandler : ITaskHandler
{
    public string TaskType => "data.transform";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            // Column references always use input (original) names; renames only change output headers.
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var select = HandlerHelpers.GetStringList(config, "select");
            foreach (var col in select)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
            }
            var drop = HandlerHelpers.GetStringList(config, "dropColumns");
            foreach (var col in drop)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
            }
            if (select.Count > 0 && drop.Count > 0)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' cannot combine 'select' and 'dropColumns'"));

            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config.TryGetProperty("renames", out var ren) && ren.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in ren.EnumerateObject())
                {
                    if (input.ColumnIndex(p.Name) < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{p.Name}' does not exist in input data"));
                    if (p.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(p.Value.GetString()))
                        return Task.FromResult(HandlerHelpers.Fail($"Rename target for '{p.Name}' must be a non-empty string"));
                    renames[p.Name] = p.Value.GetString()!;
                }
            }

            var upper = ReadColumnList(config, "upperColumns", input);
            if (upper is null) return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' references an unknown column in 'upperColumns'"));
            var lower = ReadColumnList(config, "lowerColumns", input);
            if (lower is null) return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' references an unknown column in 'lowerColumns'"));

            var dropSet = new HashSet<string>(drop, StringComparer.OrdinalIgnoreCase);
            var keepIndexes = select.Count > 0
                ? select.Select(c => input.ColumnIndex(c)).ToList()
                : Enumerable.Range(0, input.Columns.Count).Where(i => !dropSet.Contains(input.Columns[i])).ToList();

            var fill = new Dictionary<int, string>();
            if (config.TryGetProperty("fillNull", out var fillEl) && fillEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in fillEl.EnumerateObject())
                {
                    var idx = input.ColumnIndex(p.Name);
                    if (idx < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{p.Name}' does not exist in input data"));
                    if (p.Value.ValueKind == JsonValueKind.Null)
                        continue;
                    var fillValue = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.GetRawText();
                    fill[idx] = fillValue;
                }
            }

            var round = new Dictionary<int, int>();
            if (config.TryGetProperty("round", out var roundEl) && roundEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in roundEl.EnumerateObject())
                {
                    var idx = input.ColumnIndex(p.Name);
                    if (idx < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{p.Name}' does not exist in input data"));
                    if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var decimals) && decimals >= 0 && decimals <= 10)
                        round[idx] = decimals;
                }
            }

            string? concatAlias = null;
            List<int> concatOutPositions = new();
            string concatSeparator = " ";
            if (config.TryGetProperty("concat", out var concatEl) && concatEl.ValueKind == JsonValueKind.Object)
            {
                var sourceNames = new List<string>();
                if (concatEl.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sources.EnumerateArray())
                    {
                        var name = s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name) || input.ColumnIndex(name) < 0)
                            return Task.FromResult(HandlerHelpers.Fail($"Concat source '{name}' does not exist in input data"));
                        sourceNames.Add(name);
                    }
                }
                concatAlias = concatEl.TryGetProperty("alias", out var al) && al.ValueKind == JsonValueKind.String ? al.GetString() : null;
                if (concatEl.TryGetProperty("separator", out var sep) && sep.ValueKind == JsonValueKind.String)
                    concatSeparator = sep.GetString() ?? " ";
                // Sources resolve to output positions so concat sees transformed values.
                foreach (var name in sourceNames)
                {
                    var outPos = keepIndexes.FindIndex(i => input.Columns[i].Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (outPos < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Concat source '{name}' is not in the output"));
                    concatOutPositions.Add(outPos);
                }
            }

            var dataset = new Dataset();
            foreach (var i in keepIndexes)
            {
                var name = input.Columns[i];
                dataset.Columns.Add(renames.TryGetValue(name, out var renamed) ? renamed : name);
            }
            if (concatAlias is not null)
                dataset.Columns.Add(concatAlias);

            var skippedRound = 0;
            foreach (var row in input.Rows)
            {
                var outRow = new List<string?>();
                for (var k = 0; k < keepIndexes.Count; k++)
                {
                    var srcIdx = keepIndexes[k];
                    var value = srcIdx < row.Count ? row[srcIdx] : null;
                    var srcName = input.Columns[srcIdx];
                    if (value is not null)
                    {
                        if (upper.Contains(srcName)) value = value.ToUpperInvariant();
                        else if (lower.Contains(srcName)) value = value.ToLowerInvariant();
                    }
                    if (value is null && fill.TryGetValue(srcIdx, out var fillValue))
                        value = fillValue;
                    if (value is not null && round.TryGetValue(srcIdx, out var decimals))
                    {
                        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                            value = Math.Round(n, decimals).ToString(CultureInfo.InvariantCulture);
                        else
                            skippedRound++;
                    }
                    outRow.Add(value);
                }
                if (concatAlias is not null)
                {
                    outRow.Add(string.Join(concatSeparator, concatOutPositions.Select(p => p < outRow.Count ? outRow[p] ?? string.Empty : string.Empty)));
                }
                dataset.Rows.Add(outRow);
            }

            dataset.Quality.InputCount = input.Rows.Count;
            var note = skippedRound > 0 ? $", {skippedRound} non-numeric values left unrounded" : string.Empty;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Transformed {input.Rows.Count} rows into {dataset.Columns.Count} columns{note}"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static HashSet<string>? ReadColumnList(JsonElement config, string name, Dataset input)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in arr.EnumerateArray())
            {
                var col = c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(col) || input.ColumnIndex(col) < 0)
                    return null;
                set.Add(col);
            }
        }
        return set;
    }
}

public class AggregateHandler : ITaskHandler
{
    public string TaskType => "data.aggregate";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var groupBy = HandlerHelpers.GetStringList(config, "groupBy");
            foreach (var col in groupBy)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Group-by column '{col}' does not exist in input data"));
            }

            if (!config.TryGetProperty("operations", out var ops) || ops.ValueKind != JsonValueKind.Array || ops.GetArrayLength() == 0)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires a non-empty 'operations' array"));

            var operations = new List<(string Column, string Operation, string Alias)>();
            foreach (var o in ops.EnumerateArray())
            {
                var oper = o.TryGetProperty("operation", out var op) && op.ValueKind == JsonValueKind.String
                    ? op.GetString()! : string.Empty;
                var col = o.TryGetProperty("column", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? string.Empty : string.Empty;
                var alias = o.TryGetProperty("alias", out var a) && a.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(a.GetString())
                    ? a.GetString()! : (oper.Equals("count", StringComparison.OrdinalIgnoreCase) ? "count" : $"{oper}_{col}");

                if (!oper.Equals("count", StringComparison.OrdinalIgnoreCase) && input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
                operations.Add((col, oper.ToLowerInvariant(), alias));
            }

            var groupIndexes = groupBy.Select(c => input.ColumnIndex(c)).ToList();
            var groups = new Dictionary<string, List<List<string?>>>();
            var groupKeys = new Dictionary<string, List<string?>>();

            foreach (var row in input.Rows)
            {
                var keyValues = groupIndexes.Select(i => i < row.Count ? row[i] ?? string.Empty : string.Empty).ToList();
                var key = string.Join("\u001f", keyValues);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<List<string?>>();
                    groups[key] = list;
                    groupKeys[key] = keyValues;
                }
                list.Add(row);
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(groupBy);
            dataset.Columns.AddRange(operations.Select(o => o.Alias));

            foreach (var (key, rows) in groups)
            {
                var outRow = new List<string?>();
                outRow.AddRange(groupKeys[key]);

                foreach (var (col, oper, _) in operations)
                {
                    if (oper == "count")
                    {
                        outRow.Add(rows.Count.ToString(CultureInfo.InvariantCulture));
                        continue;
                    }

                    if (oper == "countdistinct")
                    {
                        var colIdx = input.ColumnIndex(col);
                        var distinct = new HashSet<string>();
                        foreach (var row in rows)
                        {
                            var cell = colIdx < row.Count ? row[colIdx] : null;
                            if (cell is not null)
                                distinct.Add(cell);
                        }
                        outRow.Add(distinct.Count.ToString(CultureInfo.InvariantCulture));
                        continue;
                    }

                    var numIdx = input.ColumnIndex(col);
                    var numbers = new List<double>();
                    foreach (var row in rows)
                    {
                        var cell = numIdx < row.Count ? row[numIdx] : null;
                        if (double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                            numbers.Add(n);
                    }

                    if (numbers.Count == 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' has no numeric values for '{oper}'"));

                    numbers.Sort();
                    var value = oper switch
                    {
                        "sum" => numbers.Sum(),
                        "avg" => numbers.Average(),
                        "min" => numbers[0],
                        "max" => numbers[^1],
                        "median" => numbers.Count % 2 == 1
                            ? numbers[numbers.Count / 2]
                            : (numbers[numbers.Count / 2 - 1] + numbers[numbers.Count / 2]) / 2,
                        _ => 0
                    };
                    outRow.Add(value.ToString(CultureInfo.InvariantCulture));
                }

                dataset.Rows.Add(outRow);
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Aggregated {input.Rows.Count} rows into {dataset.Rows.Count} groups"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}

public class SortHandler : ITaskHandler
{
    public string TaskType => "data.sort";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            if (!config.TryGetProperty("orderBy", out var orderBy) || orderBy.ValueKind != JsonValueKind.Array || orderBy.GetArrayLength() == 0)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires a non-empty 'orderBy' array"));

            var keys = new List<(int Index, bool Desc)>();
            foreach (var o in orderBy.EnumerateArray())
            {
                var col = o.TryGetProperty("column", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(col) || input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Sort column '{col}' does not exist in input data"));
                var desc = o.TryGetProperty("direction", out var d) && d.ValueKind == JsonValueKind.String
                    && d.GetString()!.Equals("desc", StringComparison.OrdinalIgnoreCase);
                keys.Add((input.ColumnIndex(col), desc));
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);
            dataset.Rows.AddRange(input.Rows.OrderBy(r => r, Comparer<List<string?>>.Create((a, b) =>
            {
                foreach (var (index, desc) in keys)
                {
                    var x = index < a.Count ? a[index] : null;
                    var y = index < b.Count ? b[index] : null;
                    var cmp = CompareCells(x, y);
                    if (cmp != 0)
                        return desc ? -cmp : cmp;
                }
                return 0;
            })));

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset, $"Sorted {dataset.Rows.Count} rows"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static int CompareCells(string? x, string? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        if (double.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(y, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
            return a.CompareTo(b);
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}

public class LimitHandler : ITaskHandler
{
    public string TaskType => "data.limit";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            if (!config.TryGetProperty("count", out var countEl) || !countEl.TryGetInt32(out var count) || count < 0)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'count' of 0 or more"));
            var offset = 0;
            if (config.TryGetProperty("offset", out var offsetEl) && offsetEl.TryGetInt32(out var off) && off > 0)
                offset = off;

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);
            dataset.Rows.AddRange(input.Rows.Skip(offset).Take(count));
            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset, $"Limited to {dataset.Rows.Count} rows (offset {offset})"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}

public class DedupeHandler : ITaskHandler
{
    public string TaskType => "data.dedupe";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            List<string> columns;
            if (config.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array && cols.GetArrayLength() > 0)
            {
                columns = cols.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            else
            {
                columns = new List<string>(input.Columns);
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);
            dataset.Rows.AddRange(input.Rows);
            dataset.Quality.InputCount = input.Rows.Count;

            if (columns.Count > 0)
                HandlerHelpers.Deduplicate(dataset, columns, ctx.NodeId);

            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Deduplicated to {dataset.Rows.Count} rows ({dataset.Quality.DuplicateCount} duplicates removed)"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}

public class JoinHandler : ITaskHandler
{
    public string TaskType => "data.join";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            if (ctx.Inputs.Count != 2)
                return Task.FromResult(HandlerHelpers.Fail(
                    $"Node '{ctx.NodeId}' join requires exactly two inputs, got {ctx.Inputs.Count}"));

            var left = ctx.Inputs[0];
            var right = ctx.Inputs[1];
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var how = HandlerHelpers.GetString(config, "how", "inner").ToLowerInvariant();
            if (how != "inner" && how != "left")
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' 'how' must be 'inner' or 'left'"));

            List<string> leftKeys, rightKeys;
            if (config.TryGetProperty("on", out var on) && on.ValueKind == JsonValueKind.Array)
            {
                var cols = on.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                leftKeys = cols;
                rightKeys = cols;
            }
            else
            {
                leftKeys = HandlerHelpers.GetStringList(config, "leftOn");
                rightKeys = HandlerHelpers.GetStringList(config, "rightOn");
            }
            if (leftKeys.Count == 0 || leftKeys.Count != rightKeys.Count)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' needs matching 'on' (or 'leftOn'/'rightOn') key columns"));

            var leftIdx = new List<int>();
            foreach (var k in leftKeys)
            {
                var i = left.ColumnIndex(k);
                if (i < 0) return Task.FromResult(HandlerHelpers.Fail($"Key column '{k}' does not exist in the left input"));
                leftIdx.Add(i);
            }
            var rightIdx = new List<int>();
            foreach (var k in rightKeys)
            {
                var i = right.ColumnIndex(k);
                if (i < 0) return Task.FromResult(HandlerHelpers.Fail($"Key column '{k}' does not exist in the right input"));
                rightIdx.Add(i);
            }

            var rightLookup = new Dictionary<string, List<List<string?>>>();
            foreach (var row in right.Rows)
            {
                var key = string.Join("\u001f", rightIdx.Select(i => i < row.Count ? row[i] ?? string.Empty : string.Empty));
                if (!rightLookup.TryGetValue(key, out var list))
                {
                    list = new List<List<string?>>();
                    rightLookup[key] = list;
                }
                list.Add(row);
            }

            var rightKeySet = new HashSet<int>(rightIdx);
            var rightOutIdx = new List<int>();
            var dataset = new Dataset();
            dataset.Columns.AddRange(left.Columns);
            for (var i = 0; i < right.Columns.Count; i++)
            {
                if (rightKeySet.Contains(i))
                    continue;
                var name = right.Columns[i];
                if (dataset.Columns.Contains(name, StringComparer.OrdinalIgnoreCase))
                    name += "_right";
                dataset.Columns.Add(name);
                rightOutIdx.Add(i);
            }

            foreach (var leftRow in left.Rows)
            {
                var key = string.Join("\u001f", leftIdx.Select(i => i < leftRow.Count ? leftRow[i] ?? string.Empty : string.Empty));
                if (rightLookup.TryGetValue(key, out var matches))
                {
                    foreach (var rightRow in matches)
                    {
                        var outRow = new List<string?>();
                        for (var i = 0; i < left.Columns.Count; i++)
                            outRow.Add(i < leftRow.Count ? leftRow[i] : null);
                        foreach (var i in rightOutIdx)
                            outRow.Add(i < rightRow.Count ? rightRow[i] : null);
                        dataset.Rows.Add(outRow);
                    }
                }
                else if (how == "left")
                {
                    var outRow = new List<string?>();
                    for (var i = 0; i < left.Columns.Count; i++)
                        outRow.Add(i < leftRow.Count ? leftRow[i] : null);
                    outRow.AddRange(Enumerable.Repeat<string?>(null, rightOutIdx.Count));
                    dataset.Rows.Add(outRow);
                }
                else
                {
                    dataset.Quality.RejectedCount++;
                }
            }

            dataset.Quality.InputCount = left.Rows.Count + right.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Joined {left.Rows.Count} + {right.Rows.Count} rows into {dataset.Rows.Count} ({how} join)"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}

public class ProfileHandler : ITaskHandler
{
    public string TaskType => "data.profile";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            List<string> columns;
            if (config.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array && cols.GetArrayLength() > 0)
            {
                columns = cols.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                foreach (var col in columns)
                {
                    if (input.ColumnIndex(col) < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
                }
            }
            else
            {
                columns = new List<string>(input.Columns);
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(new[] { "column", "count", "nullCount", "distinctCount", "min", "max", "mean" });

            foreach (var col in columns)
            {
                var idx = input.ColumnIndex(col);
                var values = input.Rows.Select(r => idx < r.Count ? r[idx] : null).ToList();
                var nonNull = values.Where(v => v is not null).Select(v => v!).ToList();
                var numbers = nonNull
                    .Where(v => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
                    .ToList();

                dataset.Rows.Add(new List<string?>
                {
                    col,
                    values.Count.ToString(CultureInfo.InvariantCulture),
                    (values.Count - nonNull.Count).ToString(CultureInfo.InvariantCulture),
                    nonNull.Distinct().Count().ToString(CultureInfo.InvariantCulture),
                    numbers.Count > 0 ? numbers.Min().ToString(CultureInfo.InvariantCulture) : nonNull.Count > 0 ? nonNull.Min() : null,
                    numbers.Count > 0 ? numbers.Max().ToString(CultureInfo.InvariantCulture) : nonNull.Count > 0 ? nonNull.Max() : null,
                    numbers.Count > 0 ? numbers.Average().ToString(CultureInfo.InvariantCulture) : null
                });
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset, $"Profiled {columns.Count} columns over {input.Rows.Count} rows"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}

public class OutputHandler : ITaskHandler
{
    private readonly IArtifactStore _artifacts;

    public OutputHandler(IArtifactStore artifacts) => _artifacts = artifacts;

    public string TaskType => "data.output";
    public bool SupportsRetry => false;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            var format = HandlerHelpers.GetString(config, "format", "json");
            var fileName = HandlerHelpers.GetString(config, "fileName");
            var delimiterText = HandlerHelpers.GetString(config, "delimiter", ",");
            var includeHeader = true;
            if (config.TryGetProperty("includeHeader", out var ih) && ih.ValueKind == JsonValueKind.False)
                includeHeader = false;

            var fileNameSaved = await _artifacts.SaveAsync(ctx.WorkflowRunId, ctx.NodeId, input, format,
                string.IsNullOrWhiteSpace(fileName) ? null : fileName,
                delimiterText.Length == 1 ? delimiterText[0] : ',',
                includeHeader, ct);
            input.Quality.OutputCount = input.Rows.Count;
            var json = input.ToJson();
            if (json.Length > Dataset.MaxStoredChars)
                return new TaskExecutionResult(false, "Result too large to store", null, null);

            return new TaskExecutionResult(true, null, json,
                $"Output saved as {fileNameSaved} [{input.Summary()}]");
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }
    }
}

public class HttpRequestHandler : ITaskHandler
{
    public const int MaxResponseBytes = 2 * 1024 * 1024;
    public const int MaxRedirects = 3;

    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "proxy-authorization", "proxy-authenticate",
        "cookie", "host", "content-length", "transfer-encoding"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public HttpRequestHandler(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public string TaskType => "http.request";
    public bool SupportsRetry => true;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        JsonElement config;
        string url;
        int timeoutSeconds;
        Dictionary<string, string> headers = new();
        string? rootPath = null;
        PaginationOptions? pagination = null;
        try
        {
            config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            url = HandlerHelpers.GetString(config, "url");
            if (string.IsNullOrWhiteSpace(url))
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'url'");
            timeoutSeconds = 30;
            if (config.TryGetProperty("timeoutSeconds", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out var parsed))
                timeoutSeconds = Math.Clamp(parsed, 1, 120);
            if (config.TryGetProperty("headers", out var hh) && hh.ValueKind == JsonValueKind.Object)
            {
                foreach (var h in hh.EnumerateObject())
                {
                    if (h.Value.ValueKind == JsonValueKind.String && !BlockedHeaders.Contains(h.Name))
                        headers[h.Name] = h.Value.GetString() ?? string.Empty;
                }
            }
            var rp = HandlerHelpers.GetString(config, "rootPath");
            if (!string.IsNullOrWhiteSpace(rp))
                rootPath = rp;
            if (config.TryGetProperty("pagination", out var pg) && pg.ValueKind == JsonValueKind.Object)
            {
                var mode = pg.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : "offset";
                if (!string.Equals(mode, "offset", StringComparison.OrdinalIgnoreCase))
                    return HandlerHelpers.Fail($"Node '{ctx.NodeId}' pagination mode '{mode}' is not supported");
                pagination = new PaginationOptions(
                    Param: pg.TryGetProperty("param", out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()) ? p.GetString()! : "page",
                    Start: pg.TryGetProperty("start", out var s) && s.TryGetInt32(out var sv) ? sv : 1,
                    Step: pg.TryGetProperty("step", out var st) && st.TryGetInt32(out var stv) && stv >= 1 ? stv : 1,
                    MaxPages: pg.TryGetProperty("maxPages", out var mp) && mp.TryGetInt32(out var mpv) ? Math.Clamp(mpv, 1, 20) : 5,
                    EndWhenEmpty: !pg.TryGetProperty("endWhenEmpty", out var ewe) || ewe.ValueKind != JsonValueKind.False);
            }
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }

        var allowPrivate = _configuration.GetValue<bool>("WorkflowExecution:AllowPrivateNetwork");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var client = _httpClientFactory.CreateClient("reflow-http");
            var allPages = new List<Dataset>();
            var totalBytes = 0L;
            var pages = pagination is null ? new[] { (Url: url, Page: (int?)null) } : BuildPageUrls(url, pagination);

            foreach (var (pageUrl, _) in pages)
            {
                await SsrfGuard.AssertSafeAsync(pageUrl, allowPrivate, timeoutCts.Token);
                var body = await FetchUrl(client, pageUrl, headers, timeoutCts.Token);
                if (body is null)
                    return HandlerHelpers.Fail($"No response received for '{TrimUrl(pageUrl)}'", true);
                totalBytes += body.Length;

                Dataset dataset;
                try
                {
                    dataset = JsonDataset.FromJsonText(body, rootPath, ctx.NodeId);
                }
                catch (InvalidOperationException ex)
                {
                    return HandlerHelpers.Fail(ex.Message);
                }
                allPages.Add(dataset);

                if (pagination is not null && pagination.EndWhenEmpty && dataset.Rows.Count == 0)
                    break;
                if (allPages.Sum(d => d.Rows.Count) > Dataset.MaxRows)
                    return HandlerHelpers.Fail($"Response has too many records (max {Dataset.MaxRows})");
            }

            var merged = allPages.Count == 1 ? allPages[0] : Dataset.Merge(allPages);
            return HandlerHelpers.Ok(merged, pages.Length > 1
                ? $"HTTP: {pages.Length} pages, {totalBytes} bytes"
                : $"HTTP: {totalBytes} bytes");
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return HandlerHelpers.Fail($"Request timed out after {timeoutSeconds}s", true);
        }
        catch (HttpRequestException ex)
        {
            return HandlerHelpers.Fail($"Request failed: {Trim(ex.Message)}", true);
        }
    }

    private async Task<string?> FetchUrl(HttpClient client, string url, Dictionary<string, string> headers, CancellationToken ct)
    {
        var currentUrl = url;
        HttpResponseMessage? response = null;
        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", "Reflow/1.0");
            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);

            response?.Dispose();
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
            {
                var next = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.ToString()
                    : new Uri(new Uri(currentUrl), response.Headers.Location).ToString();
                await SsrfGuard.AssertSafeAsync(next, _configuration.GetValue<bool>("WorkflowExecution:AllowPrivateNetwork"), ct);
                currentUrl = next;
                continue;
            }

            break;
        }

        using (response)
        {
            if (response is null)
                return null;

            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > MaxResponseBytes)
                throw new InvalidOperationException($"Response too large ({bytes.Length} bytes, max {MaxResponseBytes})");

            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }

    private static (string Url, int? Page)[] BuildPageUrls(string url, PaginationOptions pagination)
    {
        var pages = new List<(string, int?)>();
        for (var i = 0; i < pagination.MaxPages; i++)
        {
            var pageValue = pagination.Start + i * pagination.Step;
            var sep = url.Contains('?') ? "&" : "?";
            pages.Add((url + sep + Uri.EscapeDataString(pagination.Param) + "=" + pageValue, pageValue));
        }
        return pages.ToArray();
    }

    private static string Trim(string message)
    {
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length > 200 ? oneLine[..200] : oneLine;
    }

    private static string TrimUrl(string url) => url.Length > 120 ? url[..120] + "..." : url;

    internal static Dataset? JsonToDataset(string body, string nodeId)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Object || root.ValueKind == JsonValueKind.Array)
                return JsonDataset.FromElement(root, nodeId);
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record PaginationOptions(string Param, int Start, int Step, int MaxPages, bool EndWhenEmpty);
}

public class TriggerPayloadHandler : ITaskHandler
{
    public string TaskType => "trigger.payload";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ctx.TriggerPayload))
                return Task.FromResult(HandlerHelpers.Fail(
                    $"Node '{ctx.NodeId}' needs a trigger payload, but this run was started manually"));

            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            var rootPath = HandlerHelpers.GetString(config, "rootPath");
            var dataset = JsonDataset.FromJsonText(ctx.TriggerPayload,
                string.IsNullOrWhiteSpace(rootPath) ? null : rootPath, ctx.NodeId);

            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Trigger payload parsed: {dataset.Rows.Count} records"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }
}
