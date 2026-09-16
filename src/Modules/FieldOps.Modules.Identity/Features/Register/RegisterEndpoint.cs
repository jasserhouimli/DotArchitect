using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/register", async (
            HttpContext http,
            RegisterRequest request,
            UserManager<User> userManager,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var result = await RegisterHandler.Handle(request, userManager, tokenService, ct);

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

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("Register")
        .Produces(201)
        .Produces(409);
    }
}
