using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Features.ListAnalyses;

public class ListAnalysesHandler(AnalysisDbContext db)
{
    public async Task<Result<List<Domain.Analysis>>> Handle(Guid workspaceId, CancellationToken ct)
    {
        var analyses = await db.Analyses
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

        return Result<List<Domain.Analysis>>.Success(analyses);
    }
}
