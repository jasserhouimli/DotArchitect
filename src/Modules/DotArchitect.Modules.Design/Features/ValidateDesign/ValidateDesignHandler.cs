using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.ValidateDesign;

public record DesignValidationResult(bool IsValid, List<string> Errors, List<string> Warnings);

public class ValidateDesignHandler(DesignDbContext db)
{
    public async Task<Result<DesignValidationResult>> Handle(Guid designId, CancellationToken ct)
    {
        var design = await db.Designs.FindAsync([designId], ct);
        if (design is null)
            return Result<DesignValidationResult>.Failure("Design not found.", 404);

        var projects = await db.ProjectDefinitions.Where(p => p.DesignId == designId).ToListAsync(ct);
        var references = await db.ProjectReferenceDefinitions.Where(r => r.DesignId == designId).ToListAsync(ct);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (projects.Count == 0)
            warnings.Add("Design has no projects.");

        var nameGroups = projects.GroupBy(p => p.Name).Where(g => g.Count() > 1);
        foreach (var group in nameGroups)
            errors.Add($"Duplicate project name: '{group.Key}'.");

        var pathGroups = projects.GroupBy(p => p.RelativePath).Where(g => g.Count() > 1);
        foreach (var group in pathGroups)
            errors.Add($"Duplicate project path: '{group.Key}'.");

        var projectIds = projects.Select(p => p.Id).ToHashSet();
        foreach (var reference in references)
        {
            if (!projectIds.Contains(reference.SourceProjectDefinitionId))
                errors.Add($"Reference source project not found: {reference.SourceProjectDefinitionId}.");
            if (!projectIds.Contains(reference.TargetProjectDefinitionId))
                errors.Add($"Reference target project not found: {reference.TargetProjectDefinitionId}.");
        }

        var adjList = references.GroupBy(r => r.SourceProjectDefinitionId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.TargetProjectDefinitionId).ToList());

        foreach (var project in projects)
        {
            if (adjList.TryGetValue(project.Id, out var targets))
            {
                if (targets.Contains(project.Id))
                    errors.Add($"Project '{project.Name}' has a self-reference.");
            }
        }

        return Result<DesignValidationResult>.Success(
            new DesignValidationResult(errors.Count == 0, errors, warnings));
    }
}
