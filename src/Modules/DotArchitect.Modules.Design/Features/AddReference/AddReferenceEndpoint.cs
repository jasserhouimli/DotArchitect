using DotArchitect.Modules.Design.Features.AddReference;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.AddReference;

public static class AddReferenceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/designs/{designId:guid}/references", async (
            Guid designId,
            AddReferenceRequest request,
            AddReferenceHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(designId, request, ct);
            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/designs/{designId}/references/{result.Value}", new { id = result.Value }),
                400 => Results.BadRequest(new { error = result.Error }),
                404 => Results.NotFound(new { error = result.Error }),
                409 => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("AddReference")
        .WithTags("Design");
    }
}
