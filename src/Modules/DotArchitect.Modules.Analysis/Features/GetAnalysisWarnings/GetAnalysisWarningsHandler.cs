using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Domain;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Features.GetAnalysisWarnings;

public class GetAnalysisWarningsHandler(AnalysisDbContext db)
{
    public async Task<Result<List<AnalysisWarning>>> Handle(Guid analysisId, CancellationToken ct)
    {
        var warnings = await db.AnalysisWarnings
            .Where(w => w.AnalysisId == analysisId)
            .OrderBy(w => w.Code)
            .ToListAsync(ct);

        return Result<List<AnalysisWarning>>.Success(warnings);
    }
}
