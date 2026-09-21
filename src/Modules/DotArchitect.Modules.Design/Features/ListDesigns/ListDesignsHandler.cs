using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Design.Domain;
using DotArchitect.Modules.Design.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Features.ListDesigns;

public class ListDesignsHandler(DesignDbContext db)
{
    public async Task<Result<List<SolutionDesign>>> Handle(Guid workspaceId, CancellationToken ct)
    {
        var designs = await db.Designs
            .Where(d => d.WorkspaceId == workspaceId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return Result<List<SolutionDesign>>.Success(designs);
    }
}
