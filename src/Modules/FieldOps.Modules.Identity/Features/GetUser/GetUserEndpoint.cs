using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/users/{id:guid}", [Authorize] async (
            GetUserHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetUserQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetUser")
        .RequireAuthorization()
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }
}
