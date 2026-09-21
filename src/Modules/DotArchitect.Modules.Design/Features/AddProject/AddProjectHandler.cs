using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.AddProject;

public class AddProjectHandler(DesignDbContext db)
{
    public async Task<Result<Guid>> Handle(Guid designId, AddProjectRequest request, CancellationToken ct)
    {
        var design = await db.Designs.FindAsync([designId], ct);
        if (design is null)
            return Result<Guid>.Failure("Design not found.", 404);

        var nameConflict = await db.ProjectDefinitions
            .AnyAsync(p => p.DesignId == designId && p.Name == request.Name, ct);
        if (nameConflict)
            return Result<Guid>.Failure($"A project named '{request.Name}' already exists.", 409);

        var pathConflict = await db.ProjectDefinitions
            .AnyAsync(p => p.DesignId == designId && p.RelativePath == request.RelativePath, ct);
        if (pathConflict)
            return Result<Guid>.Failure($"A project at '{request.RelativePath}' already exists.", 409);

        var project = new ProjectDefinition
        {
            Id = Guid.NewGuid(),
            DesignId = designId,
            Name = request.Name,
            RelativePath = request.RelativePath,
            TemplateType = request.TemplateType,
            TargetFramework = request.TargetFramework
        };

        db.ProjectDefinitions.Add(project);

        design.Revision++;
        design.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(project.Id, 201);
    }
}
