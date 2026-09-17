using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/logout", async (
            HttpContext http,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("FieldOps.RefreshToken", out var refreshToken);

            if (!string.IsNullOrEmpty(refreshToken))
                await tokenService.RevokeRefreshTokenAsync(refreshToken, ct);

            http.Response.Cookies.Delete("FieldOps.Token");
            http.Response.Cookies.Delete("FieldOps.RefreshToken");

            return Results.Ok(new { message = "Logged out successfully" });
        })
        .WithName("Logout")
        .RequireRateLimiting("auth")
        .Produces(200);
    }
}
