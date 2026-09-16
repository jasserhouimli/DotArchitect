using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/logout", async (
            HttpContext http,
            LogoutRequest request,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var result = await LogoutHandler.Handle(request, tokenService, http, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("Logout")
        .Produces(200);
    }
}
