using DotArchitect.Modules.Analysis.Features.UploadSolution;
using DotArchitect.Modules.Analysis.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Analysis.Features.UploadSolution;

public static class UploadSolutionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workspaces/{workspaceId:guid}/analyses", async (
            Guid workspaceId,
            IFormFile file,
            UploadSolutionHandler handler,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded." });

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Only ZIP files are supported." });

            using var stream = file.OpenReadStream();
            var result = await handler.Handle(new UploadSolutionRequest(workspaceId), stream, file.FileName, ct);

            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/analyses/{result.Value}", new { id = result.Value }),
                400 => Results.BadRequest(new { error = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithName("UploadSolution")
        .WithTags("Analysis");
    }
}
