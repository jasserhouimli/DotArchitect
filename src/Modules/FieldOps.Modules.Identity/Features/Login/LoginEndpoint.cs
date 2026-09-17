using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.Login;

public static class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/login", async (
            HttpContext http,
            LoginRequest request,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var result = await LoginHandler.Handle(request, userManager, signInManager, tokenService, ct);

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
