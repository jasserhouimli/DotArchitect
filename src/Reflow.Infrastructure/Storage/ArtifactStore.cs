using System.Text;
using Reflow.Infrastructure.Data;

namespace Reflow.Infrastructure.Storage;

public interface IArtifactStore
{
    Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, string? fileName, char delimiter, bool includeHeader, CancellationToken ct);
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

    public async Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, string? fileName, char delimiter, bool includeHeader, CancellationToken ct)
    {
        var safeNode = MakeSafe(nodeId);
        var dir = Path.Combine(_root, runId.ToString("N"));
        Directory.CreateDirectory(dir);

        string actualName, content, contentType;
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            actualName = string.IsNullOrWhiteSpace(fileName) ? safeNode + ".csv" : EnsureExtension(MakeSafe(fileName), ".csv");
            content = ToCsv(dataset, delimiter, includeHeader);
            contentType = "text/csv";
        }
        else
        {
            actualName = string.IsNullOrWhiteSpace(fileName) ? safeNode + ".json" : EnsureExtension(MakeSafe(fileName), ".json");
            content = dataset.ToJson();
            contentType = "application/json";
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        if (bytes.Length > MaxArtifactBytes)
            throw new InvalidOperationException($"Output too large ({bytes.Length} bytes, max {MaxArtifactBytes})");

        var path = Path.Combine(dir, actualName);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
        if (!actualName.Equals(safeNode + (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? ".csv" : ".json"), StringComparison.OrdinalIgnoreCase))
            await File.WriteAllTextAsync(Path.Combine(dir, safeNode + ".ref"), actualName, ct);
        return actualName;
    }

    public async Task<(string Content, string ContentType, string FileName)?> LoadAsync(Guid runId, string nodeId, CancellationToken ct)
    {
        var dir = Path.Combine(_root, runId.ToString("N"));
        if (!Directory.Exists(dir))
            return null;

        var safeNode = MakeSafe(nodeId);
        var refPath = Path.Combine(dir, safeNode + ".ref");
        if (File.Exists(refPath))
        {
            var referenced = (await File.ReadAllTextAsync(refPath, ct)).Trim();
            var refFull = Path.Combine(dir, Path.GetFileName(referenced));
            if (File.Exists(refFull) && IsDataFile(refFull))
                return await ReadDataFile(refFull, ct);
        }

        var match = Directory.GetFiles(dir)
            .Select(p => new FileInfo(p))
            .Where(f => IsDataFile(f.FullName)
                && f.Name.StartsWith(safeNode + ".", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (match is null)
            return null;

        return await ReadDataFile(match.FullName, ct);
    }

    private static bool IsDataFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase) || ext.Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string Content, string ContentType, string FileName)?> ReadDataFile(string path, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(path, ct);
        var name = Path.GetFileName(path);
        return (content, Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/json", name);
    }

    private static string MakeSafe(string nodeId)
    {
        var sb = new StringBuilder();
        foreach (var ch in nodeId)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        var safe = sb.ToString().Trim('_', '.');
        return string.IsNullOrEmpty(safe) ? "node" : safe[..Math.Min(safe.Length, 80)];
    }

    private static string EnsureExtension(string name, string ext)
        => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;

    private static string ToCsv(Dataset dataset, char delimiter, bool includeHeader)
    {
        var sep = delimiter.ToString();
        var sb = new StringBuilder();
        if (includeHeader)
            sb.AppendLine(string.Join(sep, dataset.Columns.Select(c => Escape(c, delimiter))));
        foreach (var row in dataset.Rows)
        {
            var cells = new string[dataset.Columns.Count];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = Escape(i < row.Count ? row[i] : null, delimiter);
            sb.AppendLine(string.Join(sep, cells));
        }
        return sb.ToString();
    }

    private static string Escape(string? value, char delimiter)
    {
        if (value is null) return string.Empty;
        if (value.Contains('"') || value.Contains(delimiter) || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
