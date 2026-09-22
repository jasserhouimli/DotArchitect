using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.Artifacts;

public class GetArtifactHandler(WorkflowExecutionDbContext db, IArtifactStore artifacts)
{
    public async Task<Result<(string Content, string ContentType, string FileName)>> Handle(
        Guid runId, string nodeId, Guid userId, CancellationToken ct)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == runId && r.CreatedBy == userId, ct);
        if (run is null) return Result<(string, string, string)>.Failure("Run not found", 404);

        var loaded = await artifacts.LoadAsync(runId, nodeId, ct);
        if (loaded is null) return Result<(string, string, string)>.Failure("Artifact not found", 404);

        return Result<(string, string, string)>.Success(loaded.Value);
    }
}
