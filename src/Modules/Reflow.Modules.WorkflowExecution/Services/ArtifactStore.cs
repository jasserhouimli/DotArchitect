using System.Text;
using Reflow.Modules.WorkflowExecution.Data;

namespace Reflow.Modules.WorkflowExecution.Services;

public interface IArtifactStore
{
    Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, CancellationToken ct);
    Task<(string Content, string ContentType, string FileName)?> LoadAsync(Guid runId, string nodeId, CancellationToken ct);
}

public class LocalArtifactStore : IArtifactStore
{
    public const long MaxArtifactBytes = 10 * 1024 * 1024;

    private readonly string _root;

    public LocalArtifactStore()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "artifacts");
    }

    public async Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, CancellationToken ct)
    {
        var safeNode = MakeSafe(nodeId);
        var dir = Path.Combine(_root, runId.ToString("N"));
        Directory.CreateDirectory(dir);

        string fileName, content, contentType;
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            fileName = safeNode + ".csv";
            content = ToCsv(dataset);
            contentType = "text/csv";
        }
        else
        {
            fileName = safeNode + ".json";
            content = dataset.ToJson();
            contentType = "application/json";
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        if (bytes.Length > MaxArtifactBytes)
            throw new InvalidOperationException($"Output too large ({bytes.Length} bytes, max {MaxArtifactBytes})");

        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
        return fileName;
    }

    public async Task<(string Content, string ContentType, string FileName)?> LoadAsync(Guid runId, string nodeId, CancellationToken ct)
    {
        var dir = Path.Combine(_root, runId.ToString("N"));
        var safeNode = MakeSafe(nodeId);

        foreach (var ext in new[] { ".json", ".csv" })
        {
            var path = Path.Combine(dir, safeNode + ext);
            if (File.Exists(path))
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return (content, ext == ".csv" ? "text/csv" : "application/json", safeNode + ext);
            }
        }

        return null;
    }

    private static string MakeSafe(string nodeId)
    {
        var sb = new StringBuilder();
        foreach (var ch in nodeId)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        var safe = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(safe) ? "node" : safe[..Math.Min(safe.Length, 80)];
    }

    private static string ToCsv(Dataset dataset)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", dataset.Columns.Select(Escape)));
        foreach (var row in dataset.Rows)
        {
            var cells = new string[dataset.Columns.Count];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = Escape(i < row.Count ? row[i] : null);
            sb.AppendLine(string.Join(",", cells));
        }
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
