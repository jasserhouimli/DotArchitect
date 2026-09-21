using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace DotArchitect.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/refresh", async (
            HttpContext http,
            RefreshTokenHandler handler,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("DotArchitect.RefreshToken", out var refreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Json(new { error = "Refresh token not found" }, statusCode: 401);

            var result = await handler.Handle(new RefreshTokenRequest(refreshToken), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/auth/refresh"
            };

            http.Response.Cookies.Append("DotArchitect.Token", result.Value!.AccessToken, accessCookieOptions);
            http.Response.Cookies.Append("DotArchitect.RefreshToken", result.Value.RefreshToken, refreshCookieOptions);

            return Results.Ok(result.Value);
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth")
        .Produces(200)
        .Produces(401);
    }
}
