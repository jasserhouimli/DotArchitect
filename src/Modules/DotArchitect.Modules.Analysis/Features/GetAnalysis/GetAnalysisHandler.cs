using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Features.GetAnalysis;

public class GetAnalysisHandler(AnalysisDbContext db)
{
    public async Task<Result<Domain.Analysis>> Handle(Guid analysisId, CancellationToken ct)
    {
        var analysis = await db.Analyses.FindAsync([analysisId], ct);

        if (analysis is null)
            return Result<Domain.Analysis>.Failure("Analysis not found.", 404);

        return Result<Domain.Analysis>.Success(analysis);
    }
}
