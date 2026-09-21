using System.IO.Compression;

namespace DotArchitect.Modules.Analysis.Infrastructure.Archive;

public static class ZipArchiveValidator
{
    private const long MaxCompressedSizeBytes = 100 * 1024 * 1024; // 100 MB
    private const int MaxEntryCount = 10_000;

    public static (bool IsValid, string? Error) Validate(Stream archiveStream)
    {
        if (archiveStream.Length > MaxCompressedSizeBytes)
            return (false, "Archive exceeds the maximum allowed size of 100 MB.");

        try
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

            if (archive.Entries.Count > MaxEntryCount)
                return (false, $"Archive contains more than {MaxEntryCount} entries.");

            foreach (var entry in archive.Entries)
            {
                var fullName = entry.FullName;
                if (fullName.Contains("..") || fullName.StartsWith('/') || fullPathTraversal(fullName))
                    return (false, $"Archive contains potentially unsafe path: {fullName}");
            }
        }
        catch (InvalidDataException)
        {
            return (false, "The uploaded file is not a valid ZIP archive.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to read archive: {ex.Message}");
        }

        return (true, null);
    }

    private static bool fullPathTraversal(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int depth = 0;
        foreach (var part in parts)
        {
            if (part == "..") depth--;
            else depth++;
            if (depth < 0) return true;
        }
        return false;
    }
}
