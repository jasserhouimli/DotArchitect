using DotArchitect.Modules.Analysis.Features.GetAnalysis;
using DotArchitect.Modules.Analysis.Features.GetAnalysisProjects;
using DotArchitect.Modules.Analysis.Features.GetAnalysisWarnings;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Analysis.Features.GetAnalysis;

public static class GetAnalysisEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/analyses/{analysisId:guid}", async (
            Guid analysisId,
            GetAnalysisHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(analysisId, ct);

            return result.StatusCode switch
            {
                200 => Results.Ok(result.Value),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("GetAnalysis")
        .WithTags("Analysis");

        app.MapGet("/api/v1/analyses/{analysisId:guid}/projects", async (
            Guid analysisId,
            GetAnalysisProjectsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(analysisId, ct);
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("GetAnalysisProjects")
        .WithTags("Analysis");

        app.MapGet("/api/v1/analyses/{analysisId:guid}/warnings", async (
            Guid analysisId,
            GetAnalysisWarningsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(analysisId, ct);
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("GetAnalysisWarnings")
        .WithTags("Analysis");
    }
}
