using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/refresh", async (
            HttpContext http,
            UserManager<User> userManager,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("FieldOps.RefreshToken", out var refreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Json(new { error = "Refresh token not found" }, statusCode: 401);

            var result = await RefreshTokenHandler.Handle(new RefreshTokenRequest(refreshToken), userManager, tokenService, ct);

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

            http.Response.Cookies.Append("FieldOps.Token", result.Value!.AccessToken, accessCookieOptions);
            http.Response.Cookies.Append("FieldOps.RefreshToken", result.Value.RefreshToken, refreshCookieOptions);

            return Results.Ok(result.Value);
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth")
        .Produces(200)
        .Produces(401);
    }
}
