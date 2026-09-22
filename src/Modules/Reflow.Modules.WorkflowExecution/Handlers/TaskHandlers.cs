using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Reflow.Modules.WorkflowExecution.Data;
using Reflow.Modules.WorkflowExecution.Services;

namespace Reflow.Modules.WorkflowExecution.Handlers;

public record TaskExecutionContext(
    Guid WorkflowRunId,
    Guid TaskRunId,
    string NodeId,
    string NodeType,
    string? ConfigJson,
    IReadOnlyList<Dataset> Inputs);

public record TaskExecutionResult(
    bool IsSuccess,
    string? Error,
    string? OutputJson,
    string? Log,
    bool IsRetryable = false);

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

    public static Dataset RequireInput(TaskExecutionContext ctx)
    {
        if (ctx.Inputs.Count == 0)
            throw new InvalidOperationException($"Node '{ctx.NodeId}' has no input data; connect a source node (CSV read or HTTP request)");
        return Dataset.Merge(ctx.Inputs);
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
    public string TaskType => "data.csv.read";
    public bool SupportsRetry => false;

    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
    {
        try
        {
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            var csvText = HandlerHelpers.GetString(config, "csvText");
            if (string.IsNullOrWhiteSpace(csvText))
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'csvText' with CSV content"));

            var delimiterText = HandlerHelpers.GetString(config, "delimiter", ",");
            if (delimiterText.Length != 1)
                return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' 'delimiter' must be a single character"));

            var hasHeader = true;
            if (config.TryGetProperty("hasHeader", out var hh) && hh.ValueKind == JsonValueKind.False)
                hasHeader = false;

            var dataset = CsvParser.Parse(csvText, delimiterText[0], hasHeader);

            if (config.TryGetProperty("dedupeColumns", out var dedupe) && dedupe.ValueKind == JsonValueKind.Array)
            {
                var cols = dedupe.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (cols.Count > 0)
                    Deduplicate(dataset, cols);
            }

            return Task.FromResult(HandlerHelpers.Ok(dataset, $"CSV parsed: {dataset.Rows.Count} rows"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static void Deduplicate(Dataset dataset, List<string> columns)
    {
        var indexes = columns.Select(c => dataset.ColumnIndex(c)).ToList();
        if (indexes.Any(i => i < 0))
            throw new InvalidOperationException("dedupeColumns references an unknown column");

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

            var required = new List<string>();
            if (config.TryGetProperty("requiredColumns", out var rc) && rc.ValueKind == JsonValueKind.Array)
            {
                required.AddRange(rc.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            foreach (var col in required)
            {
                if (input.ColumnIndex(col) < 0)
                    return Task.FromResult(HandlerHelpers.Fail($"Required column '{col}' does not exist in input data"));
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);

            foreach (var row in input.Rows)
            {
                var bad = new List<string>();
                foreach (var col in required)
                {
                    var idx = input.ColumnIndex(col);
                    var value = idx < row.Count ? row[idx] : null;
                    if (string.IsNullOrWhiteSpace(value))
                        bad.Add(col);
                }

                if (bad.Count == 0)
                {
                    dataset.Rows.Add(row);
                }
                else
                {
                    dataset.Quality.RejectedCount++;
                    foreach (var col in bad)
                    {
                        var rule = $"required:{col}";
                        dataset.Quality.FailuresByRule[rule] = dataset.Quality.FailuresByRule.TryGetValue(rule, out var n) ? n + 1 : 1;
                    }
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
            if (needsValue)
            {
                if (!config.TryGetProperty("value", out var vp))
                    return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'value' for operator '{op}'"));
                value = vp.ValueKind == JsonValueKind.Null ? null : vp.ValueKind == JsonValueKind.String ? vp.GetString() : vp.GetRawText();
            }

            var dataset = new Dataset();
            dataset.Columns.AddRange(input.Columns);

            foreach (var row in input.Rows)
            {
                var cell = colIdx < row.Count ? row[colIdx] : null;
                if (Matches(cell, op, value))
                    dataset.Rows.Add(row);
                else
                    dataset.Quality.RejectedCount++;
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Filter {column} {op} '{value}': {dataset.Rows.Count} kept, {dataset.Quality.RejectedCount} removed"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static bool Matches(string? cell, string op, string? value)
    {
        return op.ToLowerInvariant() switch
        {
            "equals" => string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "contains" => (cell ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "notcontains" => !(cell ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase),
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
            var input = HandlerHelpers.RequireInput(ctx);
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);

            var select = new List<string>();
            if (config.TryGetProperty("select", out var sel) && sel.ValueKind == JsonValueKind.Array)
            {
                select.AddRange(sel.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                foreach (var col in select)
                {
                    if (input.ColumnIndex(col) < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' does not exist in input data"));
                }
            }

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

            var upper = ReadColumnList(config, "upperColumns", input, ctx.NodeId);
            if (upper is null) return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' references an unknown column in 'upperColumns'"));
            var lower = ReadColumnList(config, "lowerColumns", input, ctx.NodeId);
            if (lower is null) return Task.FromResult(HandlerHelpers.Fail($"Node '{ctx.NodeId}' references an unknown column in 'lowerColumns'"));

            var keepIndexes = select.Count > 0
                ? select.Select(c => input.ColumnIndex(c)).ToList()
                : Enumerable.Range(0, input.Columns.Count).ToList();

            var dataset = new Dataset();
            foreach (var i in keepIndexes)
            {
                var name = input.Columns[i];
                dataset.Columns.Add(renames.TryGetValue(name, out var renamed) ? renamed : name);
            }

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
                    outRow.Add(value);
                }
                dataset.Rows.Add(outRow);
            }

            dataset.Quality.InputCount = input.Rows.Count;
            return Task.FromResult(HandlerHelpers.Ok(dataset,
                $"Transformed {input.Rows.Count} rows into {dataset.Columns.Count} columns"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HandlerHelpers.Fail(ex.Message));
        }
    }

    private static HashSet<string>? ReadColumnList(JsonElement config, string name, Dataset input, string nodeId)
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

            var groupBy = new List<string>();
            if (config.TryGetProperty("groupBy", out var gb) && gb.ValueKind == JsonValueKind.Array)
            {
                groupBy.AddRange(gb.EnumerateArray()
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                foreach (var col in groupBy)
                {
                    if (input.ColumnIndex(col) < 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Group-by column '{col}' does not exist in input data"));
                }
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

                    var colIdx = input.ColumnIndex(col);
                    var numbers = new List<double>();
                    foreach (var row in rows)
                    {
                        var cell = colIdx < row.Count ? row[colIdx] : null;
                        if (double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                            numbers.Add(n);
                    }

                    if (numbers.Count == 0)
                        return Task.FromResult(HandlerHelpers.Fail($"Column '{col}' has no numeric values for '{oper}'"));

                    var value = oper switch
                    {
                        "sum" => numbers.Sum(),
                        "avg" => numbers.Average(),
                        "min" => numbers.Min(),
                        "max" => numbers.Max(),
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

            var fileName = await _artifacts.SaveAsync(ctx.WorkflowRunId, ctx.NodeId, input, format, ct);
            input.Quality.OutputCount = input.Rows.Count;
            var json = input.ToJson();
            if (json.Length > Dataset.MaxStoredChars)
                return new TaskExecutionResult(false, "Result too large to store", null, null);

            return new TaskExecutionResult(true, null, json,
                $"Output saved as {fileName} [{input.Summary()}]");
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
        string url;
        int timeoutSeconds;
        try
        {
            var config = HandlerHelpers.ParseConfig(ctx.ConfigJson, ctx.NodeId);
            url = HandlerHelpers.GetString(config, "url");
            if (string.IsNullOrWhiteSpace(url))
                return HandlerHelpers.Fail($"Node '{ctx.NodeId}' requires 'url'");
            timeoutSeconds = 30;
            if (config.TryGetProperty("timeoutSeconds", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out var parsed))
                timeoutSeconds = Math.Clamp(parsed, 1, 120);
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }

        var allowPrivate = _configuration.GetValue<bool>("WorkflowExecution:AllowPrivateNetwork");

        try
        {
            await SsrfGuard.AssertSafeAsync(url, allowPrivate, ct);
        }
        catch (InvalidOperationException ex)
        {
            return HandlerHelpers.Fail(ex.Message);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var client = _httpClientFactory.CreateClient("reflow-http");
            var currentUrl = url;

            HttpResponseMessage? response = null;
            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("User-Agent", "Reflow/1.0");

                response?.Dispose();
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                {
                    var next = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location.ToString()
                        : new Uri(new Uri(currentUrl), response.Headers.Location).ToString();
                    await SsrfGuard.AssertSafeAsync(next, allowPrivate, timeoutCts.Token);
                    currentUrl = next;
                    continue;
                }

                break;
            }

            using (response)
            {
                if (response is null)
                    return HandlerHelpers.Fail("No response received", true);

                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    return HandlerHelpers.Fail($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", true);

                if (!response.IsSuccessStatusCode)
                    return HandlerHelpers.Fail($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
                if (bytes.Length > MaxResponseBytes)
                    return HandlerHelpers.Fail($"Response too large ({bytes.Length} bytes, max {MaxResponseBytes})");

                var body = System.Text.Encoding.UTF8.GetString(bytes);
                var dataset = JsonToDataset(body, ctx.NodeId);
                if (dataset is null)
                    return HandlerHelpers.Fail("Response is not a JSON object or array");

                return HandlerHelpers.Ok(dataset, $"HTTP {(int)response.StatusCode}: {bytes.Length} bytes");
            }
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

    private static string Trim(string message)
    {
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length > 200 ? oneLine[..200] : oneLine;
    }

    internal static Dataset? JsonToDataset(string body, string nodeId)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
                return SingleRow(root);
            if (root.ValueKind == JsonValueKind.Array)
            {
                var items = root.EnumerateArray().ToList();
                if (items.Count > Dataset.MaxRows)
                    throw new InvalidOperationException($"Response has too many records (max {Dataset.MaxRows})");
                return ArrayRows(items);
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
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
