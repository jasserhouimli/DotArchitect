using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Domain;
using DotArchitect.Modules.Workspaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Workspaces.Features.ListWorkspaces;

public class ListWorkspacesHandler(WorkspacesDbContext db)
{
    public async Task<Result<List<Workspace>>> Handle(ListWorkspacesQuery query, Guid ownerId, CancellationToken ct)
    {
        var workspaces = await db.Workspaces
            .Where(w => w.OwnerId == ownerId)
            .OrderByDescending(w => w.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Result<List<Workspace>>.Success(workspaces);
    }
}
