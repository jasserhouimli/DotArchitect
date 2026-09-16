using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using FieldOps.Modules.Identity.Domain;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/users/{id:guid}", [Authorize] async (
            Guid id,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            var result = await GetUserHandler.Handle(new GetUserQuery(id), userManager, ct);

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
