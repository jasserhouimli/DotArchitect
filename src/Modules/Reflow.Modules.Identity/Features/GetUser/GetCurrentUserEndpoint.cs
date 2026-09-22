using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.Identity.Features.GetUser;

public static class GetCurrentUserEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/auth/me", [Authorize] async (
            GetCurrentUserHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(http.User, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization()
        .Produces(200)
        .Produces(401);
    }
}
