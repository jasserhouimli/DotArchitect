using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.GetDesign;

public static class GetDesignEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/designs/{designId:guid}", async (
            Guid designId,
            GetDesignHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(designId, ct);
            return result.StatusCode switch
            {
                200 => Results.Ok(result.Value),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("GetDesign")
        .WithTags("Design");
    }
}
