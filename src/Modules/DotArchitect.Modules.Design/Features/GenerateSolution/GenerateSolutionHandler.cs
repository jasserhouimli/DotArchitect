using System.IO.Compression;
using System.Text;
using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.GenerateSolution;

public class GenerateSolutionHandler(DesignDbContext db)
{
    public async Task<Result<byte[]>> Handle(Guid designId, CancellationToken ct)
    {
        var design = await db.Designs.FindAsync([designId], ct);
        if (design is null)
            return Result<byte[]>.Failure("Design not found.", 404);

        var projects = await db.ProjectDefinitions.Where(p => p.DesignId == designId).ToListAsync(ct);
        var references = await db.ProjectReferenceDefinitions.Where(r => r.DesignId == designId).ToListAsync(ct);

        if (projects.Count == 0)
            return Result<byte[]>.Failure("Design has no projects.", 400);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var slnContent = GenerateSolutionFile(design.Name, projects);
            CreateZipEntry(archive, $"{design.Name}.sln", slnContent);

            foreach (var project in projects)
            {
                var csprojContent = GenerateProjectFile(project);
                var dirPath = project.RelativePath.Replace('\\', '/');
                var lastSlash = dirPath.LastIndexOf('/');
                var directory = lastSlash > 0 ? dirPath[..lastSlash] : "";
                var fileName = lastSlash > 0 ? dirPath[(lastSlash + 1)..] : dirPath;

                if (!string.IsNullOrEmpty(directory))
                    CreateZipEntry(archive, $"{directory}/", "");

                var projectRefs = references.Where(r => r.SourceProjectDefinitionId == project.Id).ToList();
                CreateZipEntry(archive, project.RelativePath, csprojContent);

                if (project.TemplateType == "WebApi")
                {
                    var programContent = "var builder = WebApplication.CreateBuilder(args);\nvar app = builder.Build();\napp.MapGet(\"/\", () => \"Hello World!\");\napp.Run();\n";
                    var programDir = directory;
                    CreateZipEntry(archive, $"{programDir}/Program.cs", programContent);
                }
            }
        }

        return Result<byte[]>.Success(stream.ToArray());
    }

    private static string GenerateSolutionFile(string solutionName, List<Domain.ProjectDefinition> projects)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.0.31903.59");

        foreach (var project in projects)
        {
            var guid = DeterministicGuid(project.Name);
            var typeGuid = project.TemplateType switch
            {
                "TestProject" => "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
                _ => "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
            };
            sb.AppendLine($"Project(\"{typeGuid}\") = \"{project.Name}\", \"{project.RelativePath}\", \"{{{guid}}}\"");
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("  GlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("    Debug|Any CPU = Debug|Any CPU");
        sb.AppendLine("    Release|Any CPU = Release|Any CPU");
        sb.AppendLine("  EndGlobalSection");
        sb.AppendLine("  GlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var project in projects)
        {
            var guid = DeterministicGuid(project.Name);
            sb.AppendLine($"    {{{guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"    {{{guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"    {{{guid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"    {{{guid}}}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("  EndGlobalSection");
        sb.AppendLine("EndGlobal");

        return sb.ToString();
    }

    private static string GenerateProjectFile(Domain.ProjectDefinition project)
    {
        var sb = new StringBuilder();
        var sdk = project.TemplateType switch
        {
            "WebApi" => "Microsoft.NET.Sdk.Web",
            "TestProject" => "Microsoft.NET.Sdk",
            _ => "Microsoft.NET.Sdk"
        };

        sb.AppendLine($"<Project Sdk=\"{sdk}\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>" + project.TargetFramework + "</TargetFramework>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("</Project>");

        return sb.ToString();
    }

    private static string DeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes).ToString().ToUpper();
    }

    private static void CreateZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
