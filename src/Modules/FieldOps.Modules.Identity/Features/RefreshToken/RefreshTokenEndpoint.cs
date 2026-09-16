using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/refresh", async (
            HttpContext http,
            RefreshTokenRequest request,
            UserManager<User> userManager,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var response = await RefreshTokenHandler.Handle(request, userManager, tokenService, ct);

            if (response is null)
                return Results.Unauthorized();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            http.Response.Cookies.Append("FieldOps.Token", response.AccessToken, cookieOptions);

            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .Produces(200)
        .Produces(401);
    }
}
