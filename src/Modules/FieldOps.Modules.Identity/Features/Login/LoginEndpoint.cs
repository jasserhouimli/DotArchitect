using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using FieldOps.Modules.Identity.Domain;

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
            IConfiguration config,
            CancellationToken ct) =>
        {
            var response = await LoginHandler.Handle(request, userManager, signInManager, config, ct);

            if (response is null)
                return Results.Unauthorized();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            };

            http.Response.Cookies.Append("FieldOps.Token", response.Token, cookieOptions);

            return Results.Ok(response);
        })
        .WithName("Login")
        .Produces(200)
        .Produces(401);
    }
}
