using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.RemoveProject;

public class RemoveProjectHandler(DesignDbContext db)
{
    public async Task<Result> Handle(Guid designId, Guid projectId, CancellationToken ct)
    {
        var project = await db.ProjectDefinitions
            .FirstOrDefaultAsync(p => p.Id == projectId && p.DesignId == designId, ct);

        if (project is null)
            return Result.Failure("Project not found.", 404);

        var refs = await db.ProjectReferenceDefinitions
            .Where(r => r.DesignId == designId && (r.SourceProjectDefinitionId == projectId || r.TargetProjectDefinitionId == projectId))
            .ToListAsync(ct);

        db.ProjectReferenceDefinitions.RemoveRange(refs);
        db.ProjectDefinitions.Remove(project);

        var design = await db.Designs.FindAsync([designId], ct);
        if (design is not null)
        {
            design.Revision++;
            design.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(204);
    }
}
