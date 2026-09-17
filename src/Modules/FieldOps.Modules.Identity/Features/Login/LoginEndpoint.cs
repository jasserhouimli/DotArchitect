using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Identity.Features.Login;

public static class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/login", async (
            HttpContext http,
            LoginHandler handler,
            LoginRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

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
        .WithName("Login")
        .RequireRateLimiting("auth")
        .Produces(200)
        .Produces(401);
    }
}
