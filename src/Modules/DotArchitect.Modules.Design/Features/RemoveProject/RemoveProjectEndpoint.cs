using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.RemoveProject;

public static class RemoveProjectEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/designs/{designId:guid}/projects/{projectId:guid}", async (
            Guid designId, Guid projectId,
            RemoveProjectHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(designId, projectId, ct);
            return result.StatusCode switch
            {
                204 => Results.NoContent(),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("RemoveProject")
        .WithTags("Design");
    }
}
