using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Domain;
using DotArchitect.Modules.Workspaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Workspaces.Features.GetWorkspace;

public class GetWorkspaceHandler(WorkspacesDbContext db)
{
    public async Task<Result<Workspace>> Handle(GetWorkspaceQuery query, Guid ownerId, CancellationToken ct)
    {
        var workspace = await db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == query.WorkspaceId && w.OwnerId == ownerId, ct);

        if (workspace is null)
            return Result<Workspace>.Failure("Workspace not found.", 404);

        return Result<Workspace>.Success(workspace);
    }
}
