using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.UpdateProject;

public class UpdateProjectHandler(DesignDbContext db)
{
    public async Task<Result> Handle(Guid designId, Guid projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await db.ProjectDefinitions
            .FirstOrDefaultAsync(p => p.Id == projectId && p.DesignId == designId, ct);

        if (project is null)
            return Result.Failure("Project not found.", 404);

        var nameConflict = await db.ProjectDefinitions
            .AnyAsync(p => p.DesignId == designId && p.Name == request.Name && p.Id != projectId, ct);
        if (nameConflict)
            return Result.Failure($"A project named '{request.Name}' already exists.", 409);

        project.Name = request.Name;
        project.RelativePath = request.RelativePath;
        project.TemplateType = request.TemplateType;
        project.TargetFramework = request.TargetFramework;

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
