using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;

namespace DotArchitect.Modules.Design.Features.CreateDesign;

public class CreateDesignHandler(DesignDbContext db)
{
    public async Task<Result<Guid>> Handle(CreateDesignRequest request, Guid workspaceId, CancellationToken ct)
    {
        var design = new SolutionDesign
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Revision = 1
        };

        db.Designs.Add(design);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(design.Id, 201);
    }
}
