using System.Text.Json;
using Reflow.Infrastructure.Storage;

namespace Reflow.Modules.DataProcessing;

public static class NodeCatalog
{
    public const int MaxConfigChars = 20000;
    public const int MaxCsvChars = 1000000;

    public static readonly HashSet<string> SupportedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http.request", "data.csv.read", "data.json.read", "data.validate", "data.filter",
        "data.transform", "data.aggregate", "data.sort", "data.limit", "data.dedupe",
        "data.join", "data.profile", "data.output"
    };

    private static readonly HashSet<string> FilterOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "equals", "notEquals", "contains", "notContains", "startsWith", "endsWith",
        "matches", "inList", "greaterThan", "lessThan", "isEmpty", "isNotEmpty"
    };

    private static readonly HashSet<string> AggregateOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "countDistinct", "sum", "avg", "min", "max", "median"
    };

    private static readonly HashSet<string> ColumnTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "number", "integer", "boolean", "date"
    };

    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "proxy-authorization", "proxy-authenticate",
        "cookie", "host", "content-length", "transfer-encoding"
    };

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
                    var csvSource = "text";
                    if (root.TryGetProperty("source", out var csvSrc))
                    {
                        csvSource = csvSrc.ValueKind == JsonValueKind.String ? csvSrc.GetString() ?? "" : "";
                        if (!csvSource.Equals("text", StringComparison.OrdinalIgnoreCase)
                            && !csvSource.Equals("upload", StringComparison.OrdinalIgnoreCase))
                            errors.Add($"Node '{nodeId}' 'source' must be 'text' or 'upload'");
                    }
                    if (csvSource.Equals("upload", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryGetString(root, "fileId", out var fid) || !LocalUploadStore.IsValidFileId(fid ?? string.Empty))
                            errors.Add($"Node '{nodeId}' requires a valid 'fileId' when source is 'upload'");
                    }
                    else
                    {
                        if (!TryGetString(root, "csvText", out var csv) || string.IsNullOrWhiteSpace(csv))
                            errors.Add($"Node '{nodeId}' requires 'csvText' with CSV content");
                        else if (csv.Length > MaxCsvChars)
                            errors.Add($"Node '{nodeId}' csvText exceeds {MaxCsvChars} characters");
                    }
                    if (root.TryGetProperty("delimiter", out var delim))
                    {
                        if (delim.ValueKind != JsonValueKind.String || delim.GetString() is not { Length: 1 })
                            errors.Add($"Node '{nodeId}' 'delimiter' must be a single character");
                    }
                    if (root.TryGetProperty("hasHeader", out var hasHeader) && hasHeader.ValueKind != JsonValueKind.True && hasHeader.ValueKind != JsonValueKind.False)
                        errors.Add($"Node '{nodeId}' 'hasHeader' must be true or false");
                    if (root.TryGetProperty("trim", out var trim) && trim.ValueKind != JsonValueKind.True && trim.ValueKind != JsonValueKind.False)
                        errors.Add($"Node '{nodeId}' 'trim' must be true or false");
                    if (root.TryGetProperty("skipRows", out var skip) && (!skip.TryGetInt32(out var sk) || sk < 0))
                        errors.Add($"Node '{nodeId}' 'skipRows' must be 0 or more");
                    if (root.TryGetProperty("maxRows", out var maxRows) && (!maxRows.TryGetInt32(out var mr) || mr < 1 || mr > 50000))
                        errors.Add($"Node '{nodeId}' 'maxRows' must be between 1 and 50000");
                    if (root.TryGetProperty("nullValues", out var nulls)
                        && (nulls.ValueKind != JsonValueKind.Array || nulls.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String)))
                        errors.Add($"Node '{nodeId}' 'nullValues' must be an array of strings");
                    if (root.TryGetProperty("dedupeColumns", out var dedupe)
                        && (dedupe.ValueKind != JsonValueKind.Array || dedupe.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))))
                        errors.Add($"Node '{nodeId}' 'dedupeColumns' must be an array of non-empty strings");
                    break;

                case "data.json.read":
                    var jsonSource = "text";
                    if (root.TryGetProperty("source", out var jsonSrc))
                    {
                        jsonSource = jsonSrc.ValueKind == JsonValueKind.String ? jsonSrc.GetString() ?? "" : "";
                        if (!jsonSource.Equals("text", StringComparison.OrdinalIgnoreCase)
                            && !jsonSource.Equals("upload", StringComparison.OrdinalIgnoreCase)
                            && !jsonSource.Equals("input", StringComparison.OrdinalIgnoreCase))
                            errors.Add($"Node '{nodeId}' 'source' must be 'text', 'upload' or 'input'");
                    }
                    if (jsonSource.Equals("upload", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryGetString(root, "fileId", out var jfid) || !LocalUploadStore.IsValidFileId(jfid ?? string.Empty))
                            errors.Add($"Node '{nodeId}' requires a valid 'fileId' when source is 'upload'");
                    }
                    else if (jsonSource.Equals("input", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryGetString(root, "column", out var jcol) || string.IsNullOrWhiteSpace(jcol))
                            errors.Add($"Node '{nodeId}' requires 'column' when source is 'input'");
                    }
                    else if (!TryGetString(root, "jsonText", out var jtext) || string.IsNullOrWhiteSpace(jtext))
                    {
                        errors.Add($"Node '{nodeId}' requires 'jsonText' with JSON content");
                    }
                    if (root.TryGetProperty("rootPath", out var rp) && rp.ValueKind != JsonValueKind.String)
                        errors.Add($"Node '{nodeId}' 'rootPath' must be a string like 'data.orders' (leave it out to parse the whole file)");
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
                    if (root.TryGetProperty("headers", out var headers))
                    {
                        if (headers.ValueKind != JsonValueKind.Object)
                            errors.Add($"Node '{nodeId}' 'headers' must be an object of string values");
                        else foreach (var h in headers.EnumerateObject())
                        {
                            if (BlockedHeaders.Contains(h.Name))
                                errors.Add($"Node '{nodeId}' header '{h.Name}' is not allowed");
                            else if (h.Value.ValueKind != JsonValueKind.String)
                                errors.Add($"Node '{nodeId}' header '{h.Name}' must be a string");
                        }
                    }
                    if (root.TryGetProperty("rootPath", out var hrp) && hrp.ValueKind != JsonValueKind.String)
                        errors.Add($"Node '{nodeId}' 'rootPath' must be a string like 'data.orders' (leave it out to parse the whole response)");
                    if (root.TryGetProperty("pagination", out var paging))
                    {
                        if (paging.ValueKind != JsonValueKind.Object)
                        {
                            errors.Add($"Node '{nodeId}' 'pagination' must be an object");
                        }
                        else
                        {
                            if (paging.TryGetProperty("mode", out var mode)
                                && (!mode.ValueKind.Equals(JsonValueKind.String) || !mode.GetString()!.Equals("offset", StringComparison.OrdinalIgnoreCase)))
                                errors.Add($"Node '{nodeId}' pagination 'mode' must be 'offset'");
                            if (paging.TryGetProperty("param", out var param)
                                && (param.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(param.GetString())))
                                errors.Add($"Node '{nodeId}' pagination 'param' must be a non-empty query parameter name");
                            if (paging.TryGetProperty("start", out var start) && !start.TryGetInt32(out _))
                                errors.Add($"Node '{nodeId}' pagination 'start' must be an integer");
                            if (paging.TryGetProperty("step", out var step) && (!step.TryGetInt32(out var st) || st < 1))
                                errors.Add($"Node '{nodeId}' pagination 'step' must be 1 or more");
                            if (paging.TryGetProperty("maxPages", out var maxPages) && (!maxPages.TryGetInt32(out var mp) || mp < 1 || mp > 20))
                                errors.Add($"Node '{nodeId}' pagination 'maxPages' must be between 1 and 20");
                        }
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
                    if (root.TryGetProperty("columnTypes", out var colTypes))
                    {
                        if (colTypes.ValueKind != JsonValueKind.Object)
                            errors.Add($"Node '{nodeId}' 'columnTypes' must be an object like {{\"amount\": \"number\"}}");
                        else foreach (var ct in colTypes.EnumerateObject())
                        {
                            if (ct.Value.ValueKind != JsonValueKind.String || !ColumnTypes.Contains(ct.Value.GetString() ?? string.Empty))
                                errors.Add($"Node '{nodeId}' type for '{ct.Name}' must be one of: {string.Join(", ", ColumnTypes)}");
                        }
                    }
                    if (root.TryGetProperty("uniqueColumns", out var uniq)
                        && (uniq.ValueKind != JsonValueKind.Array || uniq.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))))
                        errors.Add($"Node '{nodeId}' 'uniqueColumns' must be an array of non-empty strings");
                    break;

                case "data.filter":
                    if (!TryGetString(root, "column", out var col) || string.IsNullOrWhiteSpace(col))
                        errors.Add($"Node '{nodeId}' requires 'column'");
                    if (!TryGetString(root, "operator", out var op) || !FilterOperators.Contains(op ?? string.Empty))
                        errors.Add($"Node '{nodeId}' 'operator' must be one of: {string.Join(", ", FilterOperators)}");
                    else if (op!.Equals("inList", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!root.TryGetProperty("value", out var inList) || inList.ValueKind != JsonValueKind.Array || inList.GetArrayLength() == 0)
                            errors.Add($"Node '{nodeId}' requires 'value' to be a non-empty array for operator 'inList'");
                    }
                    else if (!op.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)
                        && !op.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase)
                        && !root.TryGetProperty("value", out _))
                        errors.Add($"Node '{nodeId}' requires 'value' for operator '{op}'");
                    break;

                case "data.transform":
                    var hasSelect = root.TryGetProperty("select", out var select) && select.ValueKind == JsonValueKind.Array;
                    var hasDrop = root.TryGetProperty("dropColumns", out var drop) && drop.ValueKind == JsonValueKind.Array;
                    var hasRenames = root.TryGetProperty("renames", out var renames) && renames.ValueKind == JsonValueKind.Object;
                    var hasUpper = root.TryGetProperty("upperColumns", out var upper) && upper.ValueKind == JsonValueKind.Array;
                    var hasLower = root.TryGetProperty("lowerColumns", out var lower) && lower.ValueKind == JsonValueKind.Array;
                    var hasFill = root.TryGetProperty("fillNull", out var fill) && fill.ValueKind == JsonValueKind.Object;
                    var hasRound = root.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Object;
                    var hasConcat = root.TryGetProperty("concat", out var concat) && concat.ValueKind == JsonValueKind.Object;
                    if (hasSelect && select.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                        errors.Add($"Node '{nodeId}' 'select' must contain only non-empty strings");
                    if (hasDrop && drop.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                        errors.Add($"Node '{nodeId}' 'dropColumns' must contain only non-empty strings");
                    if (hasSelect && hasDrop)
                        errors.Add($"Node '{nodeId}' cannot combine 'select' and 'dropColumns'");
                    if (hasRound)
                    {
                        foreach (var r in round.EnumerateObject())
                        {
                            if ((r.Value.ValueKind != JsonValueKind.Number || !r.Value.TryGetInt32(out var decimals) || decimals < 0 || decimals > 10))
                                errors.Add($"Node '{nodeId}' round decimals for '{r.Name}' must be between 0 and 10");
                        }
                    }
                    if (root.TryGetProperty("concat", out var cc))
                    {
                        if (cc.ValueKind != JsonValueKind.Object)
                            errors.Add($"Node '{nodeId}' 'concat' must be an object");
                        else
                        {
                            if (!cc.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array || sources.GetArrayLength() == 0
                                || sources.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                                errors.Add($"Node '{nodeId}' 'concat.sources' must be a non-empty array of column names");
                            if (!cc.TryGetProperty("alias", out var alias) || alias.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(alias.GetString()))
                                errors.Add($"Node '{nodeId}' 'concat.alias' is required");
                        }
                    }
                    if (!hasSelect && !hasDrop && !hasRenames && !hasUpper && !hasLower && !hasFill && !hasRound && !hasConcat)
                        warnings?.Add($"Node '{nodeId}' has no transform operations (select, dropColumns, renames, upperColumns, lowerColumns, fillNull, round, concat)");
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

                case "data.sort":
                    if (!root.TryGetProperty("orderBy", out var orderBy) || orderBy.ValueKind != JsonValueKind.Array || orderBy.GetArrayLength() == 0)
                    {
                        errors.Add($"Node '{nodeId}' requires a non-empty 'orderBy' array");
                    }
                    else foreach (var o in orderBy.EnumerateArray())
                    {
                        if (o.ValueKind != JsonValueKind.Object
                            || !o.TryGetProperty("column", out var scol) || scol.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(scol.GetString()))
                        {
                            errors.Add($"Node '{nodeId}' each 'orderBy' entry requires 'column'");
                            continue;
                        }
                        if (o.TryGetProperty("direction", out var dir) && dir.ValueKind == JsonValueKind.String
                            && !dir.GetString()!.Equals("asc", StringComparison.OrdinalIgnoreCase)
                            && !dir.GetString()!.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            errors.Add($"Node '{nodeId}' direction for '{scol.GetString()}' must be 'asc' or 'desc'");
                    }
                    break;

                case "data.limit":
                    if (!root.TryGetProperty("count", out var count) || !count.TryGetInt32(out var cc2) || cc2 < 0)
                        errors.Add($"Node '{nodeId}' requires 'count' of 0 or more");
                    if (root.TryGetProperty("offset", out var offset) && (!offset.TryGetInt32(out var off) || off < 0))
                        errors.Add($"Node '{nodeId}' 'offset' must be 0 or more");
                    break;

                case "data.dedupe":
                    if (root.TryGetProperty("columns", out var dedupeCols)
                        && (dedupeCols.ValueKind != JsonValueKind.Array || dedupeCols.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))))
                        errors.Add($"Node '{nodeId}' 'columns' must be an array of non-empty strings (empty means all columns)");
                    break;

                case "data.join":
                    if (root.TryGetProperty("how", out var how)
                        && (how.ValueKind != JsonValueKind.String
                            || (!how.GetString()!.Equals("inner", StringComparison.OrdinalIgnoreCase)
                                && !how.GetString()!.Equals("left", StringComparison.OrdinalIgnoreCase))))
                        errors.Add($"Node '{nodeId}' 'how' must be 'inner' or 'left'");
                    var hasOn = root.TryGetProperty("on", out var onCols) && onCols.ValueKind == JsonValueKind.Array;
                    var hasSides = root.TryGetProperty("leftOn", out _) || root.TryGetProperty("rightOn", out _);
                    if (hasOn && hasSides)
                    {
                        errors.Add($"Node '{nodeId}' cannot combine 'on' with 'leftOn'/'rightOn'");
                    }
                    else if (hasOn)
                    {
                        if (onCols.GetArrayLength() == 0 || onCols.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                            errors.Add($"Node '{nodeId}' 'on' must be a non-empty array of column names");
                    }
                    else
                    {
                        if (!root.TryGetProperty("leftOn", out var leftOn) || leftOn.ValueKind != JsonValueKind.Array || leftOn.GetArrayLength() == 0
                            || leftOn.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                            errors.Add($"Node '{nodeId}' requires 'leftOn' (or shorthand 'on')");
                        if (!root.TryGetProperty("rightOn", out var rightOn) || rightOn.ValueKind != JsonValueKind.Array || rightOn.GetArrayLength() == 0
                            || rightOn.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())))
                            errors.Add($"Node '{nodeId}' requires 'rightOn' (or shorthand 'on')");
                    }
                    break;

                case "data.profile":
                    if (root.TryGetProperty("columns", out var profCols)
                        && (profCols.ValueKind != JsonValueKind.Array || profCols.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))))
                        errors.Add($"Node '{nodeId}' 'columns' must be an array of non-empty strings (empty means all columns)");
                    break;

                case "data.output":
                    if (root.TryGetProperty("format", out var format))
                    {
                        var f = format.ValueKind == JsonValueKind.String ? format.GetString() : null;
                        if (!string.Equals(f, "json", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(f, "csv", StringComparison.OrdinalIgnoreCase))
                            errors.Add($"Node '{nodeId}' 'format' must be 'json' or 'csv'");
                    }
                    if (root.TryGetProperty("fileName", out var fileName) && fileName.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(fileName.GetString()))
                    {
                        var fn = fileName.GetString()!;
                        if (fn.Length > 80 || fn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                            || fn.Contains('/') || fn.Contains('\\'))
                            errors.Add($"Node '{nodeId}' 'fileName' must be a safe file name (max 80 chars, no path separators)");
                    }
                    else if (root.TryGetProperty("fileName", out var fileNameBad) && fileNameBad.ValueKind != JsonValueKind.String
                        && fileNameBad.ValueKind != JsonValueKind.Null)
                    {
                        errors.Add($"Node '{nodeId}' 'fileName' must be a string");
                    }
                    if (root.TryGetProperty("delimiter", out var outDelim)
                        && (outDelim.ValueKind != JsonValueKind.String || outDelim.GetString() is not { Length: 1 }))
                        errors.Add($"Node '{nodeId}' 'delimiter' must be a single character");
                    if (root.TryGetProperty("includeHeader", out var incl) && incl.ValueKind != JsonValueKind.True && incl.ValueKind != JsonValueKind.False)
                        errors.Add($"Node '{nodeId}' 'includeHeader' must be true or false");
                    break;
            }
        }

        return errors;
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
