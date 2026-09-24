using System.Text.Json;
using System.Text.RegularExpressions;
using Reflow.Infrastructure.Data;

namespace Reflow.Infrastructure.Storage;

public record UploadedFile(string FileId, string FileName, long Size, int Rows, string[] Columns, DateTime UploadedAt);

public interface IUploadStore
{
    Task<UploadedFile> SaveAsync(Guid workflowId, string fileName, Stream content, CancellationToken ct);
    Task<IReadOnlyList<UploadedFile>> ListAsync(Guid workflowId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid workflowId, string fileId, CancellationToken ct);
    Task<string?> ReadTextAsync(Guid workflowId, string fileId, CancellationToken ct);
    bool IsValidFileId(string fileId);
}

public class LocalUploadStore : IUploadStore
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = [".csv", ".json", ".txt"];

    private static readonly Regex FileIdPattern = new("^[0-9a-f]{32}\\.(csv|json|txt)$", RegexOptions.Compiled);

    public static bool IsValidFileId(string fileId) => FileIdPattern.IsMatch(fileId);

    private readonly string _root;

    public LocalUploadStore(string? root = null)
    {
        _root = root ?? Path.Combine(AppContext.BaseDirectory, "artifacts", "uploads");
    }

    bool IUploadStore.IsValidFileId(string fileId) => IsValidFileId(fileId);

    public async Task<UploadedFile> SaveAsync(Guid workflowId, string fileName, Stream content, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not allowed (csv, json, txt only)");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
            throw new InvalidOperationException("Uploaded file is empty");
        if (buffer.Length > MaxUploadBytes)
            throw new InvalidOperationException($"File too large ({buffer.Length} bytes, max {MaxUploadBytes})");

        var dir = WorkflowDir(workflowId);
        Directory.CreateDirectory(dir);

        var fileId = $"{Guid.NewGuid():N}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(dir, fileId), buffer.ToArray(), ct);

        var text = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        var (rows, columns) = Sniff(text, ext);

        var meta = new UploadedFile(fileId, MakeSafeName(fileName), buffer.Length, rows, columns, DateTime.UtcNow);
        await File.WriteAllTextAsync(Path.Combine(dir, fileId + ".meta.json"), JsonSerializer.Serialize(meta), ct);
        return meta;
    }

    public Task<IReadOnlyList<UploadedFile>> ListAsync(Guid workflowId, CancellationToken ct)
    {
        var dir = WorkflowDir(workflowId);
        var result = new List<UploadedFile>();
        if (!Directory.Exists(dir))
            return Task.FromResult<IReadOnlyList<UploadedFile>>(result);

        foreach (var metaPath in Directory.GetFiles(dir, "*.meta.json"))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<UploadedFile>(File.ReadAllText(metaPath));
                if (meta is not null && File.Exists(Path.Combine(dir, meta.FileId)))
                    result.Add(meta);
            }
            catch (JsonException) { }
        }

        return Task.FromResult<IReadOnlyList<UploadedFile>>(result.OrderByDescending(f => f.UploadedAt).ToList());
    }

    public Task<bool> DeleteAsync(Guid workflowId, string fileId, CancellationToken ct)
    {
        if (!IsValidFileId(fileId))
            return Task.FromResult(false);

        var dir = WorkflowDir(workflowId);
        var path = Path.Combine(dir, fileId);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        var metaPath = path + ".meta.json";
        if (File.Exists(metaPath))
            File.Delete(metaPath);
        return Task.FromResult(true);
    }

    public async Task<string?> ReadTextAsync(Guid workflowId, string fileId, CancellationToken ct)
    {
        if (!IsValidFileId(fileId))
            return null;

        var path = Path.Combine(WorkflowDir(workflowId), fileId);
        if (!File.Exists(path))
            return null;

        return await File.ReadAllTextAsync(path, ct);
    }

    private string WorkflowDir(Guid workflowId) => Path.Combine(_root, workflowId.ToString("N"));

    private static (int Rows, string[] Columns) Sniff(string text, string ext)
    {
        if (ext == ".json")
        {
            try
            {
                var json = JsonDataset.FromJsonText(text, null, "upload");
                return (json.Rows.Count, json.Columns.ToArray());
            }
            catch (InvalidOperationException)
            {
                return (0, Array.Empty<string>());
            }
        }

        var parsed = CsvParser.Parse(text, ',', true);
        return (parsed.Rows.Count, parsed.Columns.ToArray());
    }

    private static string MakeSafeName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ' ')
                sb.Append(ch);
        }
        var safe = sb.ToString().Trim();
        if (string.IsNullOrEmpty(safe))
            safe = "upload";
        return safe.Length > 100 ? safe[..100] : safe;
    }
}
