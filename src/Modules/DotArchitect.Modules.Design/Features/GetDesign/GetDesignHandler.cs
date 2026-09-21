using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.GetDesign;

public record DesignResponse(
    SolutionDesign Design,
    List<ProjectDefinition> Projects,
    List<ProjectReferenceDefinition> References,
    List<ArchitectureGroup> Groups,
    List<DesignNodeLayout> Layouts
);

public class GetDesignHandler(DesignDbContext db)
{
    public async Task<Result<DesignResponse>> Handle(Guid designId, CancellationToken ct)
    {
        var design = await db.Designs.FindAsync([designId], ct);
        if (design is null)
            return Result<DesignResponse>.Failure("Design not found.", 404);

        var projects = await db.ProjectDefinitions
            .Where(p => p.DesignId == designId).ToListAsync(ct);
        var references = await db.ProjectReferenceDefinitions
            .Where(r => r.DesignId == designId).ToListAsync(ct);
        var groups = await db.ArchitectureGroups
            .Where(g => g.DesignId == designId).ToListAsync(ct);
        var layouts = await db.DesignNodeLayouts
            .Where(l => l.DesignId == designId).ToListAsync(ct);

        return Result<DesignResponse>.Success(
            new DesignResponse(design, projects, references, groups, layouts));
    }
}
