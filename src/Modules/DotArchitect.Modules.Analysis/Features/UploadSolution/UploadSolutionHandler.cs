using System.IO.Compression;
using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Domain;
using DotArchitect.Modules.Analysis.Infrastructure.Archive;
using DotArchitect.Modules.Analysis.Infrastructure.ProjectParsing;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Features.UploadSolution;

public class UploadSolutionHandler(AnalysisDbContext db)
{
    public async Task<Result<Guid>> Handle(UploadSolutionRequest request, Stream fileStream, string fileName, CancellationToken ct)
    {
        var (isValid, error) = ZipArchiveValidator.Validate(fileStream);
        if (!isValid)
            return Result<Guid>.Failure(error!, 400);

        var analysis = new Domain.Analysis
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Status = AnalysisStatus.Processing,
            OriginalFileName = fileName,
            StartedAt = DateTime.UtcNow
        };

        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        try
        {
            fileStream.Position = 0;
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var projectFiles = ProjectFileParser.DiscoverProjectFiles(archive);
            var projectMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var warnings = new List<AnalysisWarning>();

            foreach (var projectFile in projectFiles)
            {
                var parsed = ProjectFileParser.ParseProjectFile(archive, projectFile);
                if (parsed is null)
                {
                    warnings.Add(new AnalysisWarning
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = analysis.Id,
                        Code = "UNSUPPORTED_PROJECT",
                        Message = $"Project file '{projectFile}' uses an unsupported SDK or format.",
                        RelativePath = projectFile,
                        CreatedAt = DateTime.UtcNow
                    });
                    continue;
                }

                var project = new AnalyzedProject
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    Name = parsed.Name,
                    RelativePath = parsed.RelativePath,
                    ProjectType = parsed.ProjectType,
                    TargetFrameworks = parsed.TargetFrameworks
                };

                db.AnalyzedProjects.Add(project);
                projectMap[parsed.RelativePath] = project.Id;

                if (string.IsNullOrEmpty(parsed.TargetFrameworks))
                {
                    warnings.Add(new AnalysisWarning
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = analysis.Id,
                        Code = "NO_TARGET_FRAMEWORK",
                        Message = $"Project '{parsed.Name}' does not specify a target framework.",
                        RelativePath = parsed.RelativePath,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            foreach (var projectFile in projectFiles)
            {
                var parsed = ProjectFileParser.ParseProjectFile(archive, projectFile);
                if (parsed is null || !projectMap.ContainsKey(projectFile)) continue;

                foreach (var refPath in parsed.ProjectReferences)
                {
                    var resolvedPath = ResolveProjectReference(projectFile, refPath);
                    if (!projectMap.TryGetValue(resolvedPath, out var targetId))
                    {
                        warnings.Add(new AnalysisWarning
                        {
                            Id = Guid.NewGuid(),
                            AnalysisId = analysis.Id,
                            Code = "UNRESOLVED_REFERENCE",
                            Message = $"Project '{parsed.Name}' references '{refPath}' which was not found.",
                            RelativePath = projectFile,
                            CreatedAt = DateTime.UtcNow
                        });
                        continue;
                    }

                    db.ProjectReferences.Add(new ProjectReference
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = analysis.Id,
                        SourceProjectId = projectMap[projectFile],
                        TargetProjectId = targetId
                    });
                }
            }

            db.AnalysisWarnings.AddRange(warnings);

            analysis.ProjectCount = projectMap.Count;
            analysis.ReferenceCount = await db.ProjectReferences.CountAsync(r => r.AnalysisId == analysis.Id, ct);
            analysis.WarningCount = warnings.Count;
            analysis.Status = AnalysisStatus.Completed;
            analysis.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Result<Guid>.Success(analysis.Id, 201);
        }
        catch (Exception ex)
        {
            analysis.Status = AnalysisStatus.Failed;
            analysis.ErrorCode = "PARSE_ERROR";
            await db.SaveChangesAsync(ct);

            return Result<Guid>.Failure($"Analysis failed: {ex.Message}", 500);
        }
    }

    private static string ResolveProjectReference(string sourceProjectPath, string referencePath)
    {
        var sourceDir = Path.GetDirectoryName(sourceProjectPath)?.Replace('\\', '/') ?? string.Empty;
        var resolved = Path.GetFullPath(Path.Combine(sourceDir, referencePath)).Replace('\\', '/');
        return resolved;
    }
}
