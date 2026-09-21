using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Domain;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Features.GetAnalysisProjects;

public class GetAnalysisProjectsHandler(AnalysisDbContext db)
{
    public async Task<Result<List<AnalyzedProject>>> Handle(Guid analysisId, CancellationToken ct)
    {
        var projects = await db.AnalyzedProjects
            .Where(p => p.AnalysisId == analysisId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return Result<List<AnalyzedProject>>.Success(projects);
    }
}
