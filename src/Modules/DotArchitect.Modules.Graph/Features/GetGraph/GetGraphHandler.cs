using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Analysis.Persistence;
using DotArchitect.Modules.Graph.Model;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Graph.Features.GetGraph;

public class GetGraphHandler(AnalysisDbContext db)
{
    public async Task<Result<ProjectGraph>> Handle(Guid analysisId, CancellationToken ct)
    {
        var analysis = await db.Analyses.FindAsync([analysisId], ct);
        if (analysis is null)
            return Result<ProjectGraph>.Failure("Analysis not found.", 404);

        var projects = await db.AnalyzedProjects
            .Where(p => p.AnalysisId == analysisId)
            .ToListAsync(ct);

        var references = await db.ProjectReferences
            .Where(r => r.AnalysisId == analysisId)
            .ToListAsync(ct);

        var graph = new ProjectGraph();

        foreach (var project in projects)
        {
            graph.AddNode(new GraphNode
            {
                Id = project.Id.ToString(),
                Name = project.Name,
                ProjectType = project.ProjectType,
                TargetFrameworks = project.TargetFrameworks
            });
        }

        foreach (var reference in references)
        {
            graph.AddEdge(reference.SourceProjectId.ToString(), reference.TargetProjectId.ToString());
        }

        return Result<ProjectGraph>.Success(graph);
    }
}
