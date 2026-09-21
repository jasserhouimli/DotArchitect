using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Domain;
using DotArchitect.Modules.Workspaces.Persistence;

namespace DotArchitect.Modules.Workspaces.Features.CreateWorkspace;

public class CreateWorkspaceHandler(WorkspacesDbContext db)
{
    public async Task<Result<Guid>> Handle(CreateWorkspaceRequest request, Guid ownerId, CancellationToken ct)
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(workspace.Id, 201);
    }
}
