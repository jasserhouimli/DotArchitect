using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Workspaces.Features.DeleteWorkspace;

public class DeleteWorkspaceHandler(WorkspacesDbContext db)
{
    public async Task<Result> Handle(Guid workspaceId, Guid ownerId, CancellationToken ct)
    {
        var workspace = await db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId && w.OwnerId == ownerId, ct);

        if (workspace is null)
            return Result.Failure("Workspace not found.", 404);

        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
