using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.GenerateSolution;

public static class GenerateSolutionEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/designs/{designId:guid}/generate", async (
            Guid designId,
            GenerateSolutionHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(designId, ct);
            return result.StatusCode switch
            {
                200 => Results.File(result.Value!, "application/zip", $"{designId}.zip"),
                400 => Results.BadRequest(new { error = result.Error }),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithName("GenerateSolution")
        .WithTags("Design");
    }
}
