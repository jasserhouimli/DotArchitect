using System.IO.Compression;
using System.Xml.Linq;

namespace DotArchitect.Modules.Analysis.Infrastructure.ProjectParsing;

public record ParsedProject(
    string Name,
    string RelativePath,
    string ProjectType,
    string TargetFrameworks,
    List<string> ProjectReferences
);

public static class ProjectFileParser
{
    private static readonly HashSet<string> SupportedSdks = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.NET.Sdk",
        "Microsoft.NET.Sdk.Web",
        "Microsoft.NET.Sdk.Worker",
        "Microsoft.NET.Sdk.WindowsDesktop",
        "Microsoft.NET.Sdk.BlazorWebAssembly"
    };

    public static List<string> DiscoverProjectFiles(ZipArchive archive)
    {
        var slnFiles = archive.Entries
            .Where(e => e.FullName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .ToList();

        var csprojFiles = archive.Entries
            .Where(e => e.FullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .ToList();

        return csprojFiles;
    }

    public static ParsedProject? ParseProjectFile(ZipArchive archive, string relativePath)
    {
        var entry = archive.GetEntry(relativePath);
        if (entry is null) return null;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var root = doc.Root;
        if (root is null) return null;

        var sdk = root.Attribute("Sdk")?.Value ?? string.Empty;

        if (!SupportedSdks.Contains(sdk))
            return null;

        var name = Path.GetFileNameWithoutExtension(relativePath);
        var projectType = sdk switch
        {
            "Microsoft.NET.Sdk.Web" => "WebApi",
            "Microsoft.NET.Sdk.Worker" => "Worker",
            "Microsoft.NET.Sdk.BlazorWebAssembly" => "BlazorWebAssembly",
            _ => "ClassLibrary"
        };

        var frameworks = new List<string>();
        var tfm = root.Element("PropertyGroup")?.Element("TargetFramework")?.Value;
        var tfms = root.Element("PropertyGroup")?.Element("TargetFrameworks")?.Value;
        if (!string.IsNullOrEmpty(tfm)) frameworks.Add(tfm);
        if (!string.IsNullOrEmpty(tfms)) frameworks.AddRange(tfms.Split(';', StringSplitOptions.RemoveEmptyEntries));

        var references = root.Descendants("ProjectReference")
            .Select(r => r.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .ToList();

        return new ParsedProject(
            name,
            relativePath,
            projectType,
            string.Join(", ", frameworks),
            references
        );
    }
}
