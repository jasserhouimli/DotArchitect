using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Workspaces.Features.RenameWorkspace;

public class RenameWorkspaceHandler(WorkspacesDbContext db)
{
    public async Task<Result> Handle(Guid workspaceId, RenameWorkspaceRequest request, Guid ownerId, CancellationToken ct)
    {
        var workspace = await db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId && w.OwnerId == ownerId, ct);

        if (workspace is null)
            return Result.Failure("Workspace not found.", 404);

        workspace.Name = request.Name;
        workspace.Description = request.Description;
        workspace.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
