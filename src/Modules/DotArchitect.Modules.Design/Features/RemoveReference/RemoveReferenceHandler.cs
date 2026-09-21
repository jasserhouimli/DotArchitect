using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.RemoveReference;

public class RemoveReferenceHandler(DesignDbContext db)
{
    public async Task<Result> Handle(Guid designId, Guid referenceId, CancellationToken ct)
    {
        var reference = await db.ProjectReferenceDefinitions
            .FirstOrDefaultAsync(r => r.Id == referenceId && r.DesignId == designId, ct);

        if (reference is null)
            return Result.Failure("Reference not found.", 404);

        db.ProjectReferenceDefinitions.Remove(reference);

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
