using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.AddReference;

public class AddReferenceHandler(DesignDbContext db)
{
    public async Task<Result<Guid>> Handle(Guid designId, AddReferenceRequest request, CancellationToken ct)
    {
        var design = await db.Designs.FindAsync([designId], ct);
        if (design is null)
            return Result<Guid>.Failure("Design not found.", 404);

        if (request.SourceProjectId == request.TargetProjectId)
            return Result<Guid>.Failure("A project cannot reference itself.", 400);

        var sourceExists = await db.ProjectDefinitions.AnyAsync(p => p.Id == request.SourceProjectId && p.DesignId == designId, ct);
        var targetExists = await db.ProjectDefinitions.AnyAsync(p => p.Id == request.TargetProjectId && p.DesignId == designId, ct);

        if (!sourceExists || !targetExists)
            return Result<Guid>.Failure("Source or target project not found.", 404);

        var exists = await db.ProjectReferenceDefinitions
            .AnyAsync(r => r.DesignId == designId && r.SourceProjectDefinitionId == request.SourceProjectId && r.TargetProjectDefinitionId == request.TargetProjectId, ct);
        if (exists)
            return Result<Guid>.Failure("Reference already exists.", 409);

        var reference = new ProjectReferenceDefinition
        {
            Id = Guid.NewGuid(),
            DesignId = designId,
            SourceProjectDefinitionId = request.SourceProjectId,
            TargetProjectDefinitionId = request.TargetProjectId
        };

        db.ProjectReferenceDefinitions.Add(reference);
        design.Revision++;
        design.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(reference.Id, 201);
    }
}
