using DotArchitect.Modules.Design.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.ValidateDesign;

public static class ValidateDesignEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/designs/{designId:guid}/validate", async (
            Guid designId,
            ValidateDesignHandler handler,
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
        .WithName("ValidateDesign")
        .WithTags("Design");
    }
}
