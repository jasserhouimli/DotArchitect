using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.RemoveReference;

public static class RemoveReferenceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/designs/{designId:guid}/references/{referenceId:guid}", async (
            Guid designId, Guid referenceId,
            RemoveReferenceHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(designId, referenceId, ct);
            return result.StatusCode switch
            {
                204 => Results.NoContent(),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("RemoveReference")
        .WithTags("Design");
    }
}
