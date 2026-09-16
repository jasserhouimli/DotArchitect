using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            http.Response.Cookies.Append("FieldOps.Token", result.Value!.AccessToken, cookieOptions);

            return Results.Ok(result.Value);
        })
        .WithName("Login")
        .Produces(200)
        .Produces(401);
    }
}
