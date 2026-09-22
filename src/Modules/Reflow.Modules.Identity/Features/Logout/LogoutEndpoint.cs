using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Reflow.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/auth/logout", async (
            HttpContext http,
            LogoutHandler handler,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("Reflow.RefreshToken", out var refreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Ok(new { message = "Logged out successfully" });

            var result = await handler.Handle(new LogoutRequest(refreshToken), http, ct);

            http.Response.Cookies.Delete("Reflow.Token");
            http.Response.Cookies.Delete("Reflow.RefreshToken");

            return Results.Ok(new { message = "Logged out successfully" });
        })
        .WithName("Logout")
        .RequireRateLimiting("auth")
        .Produces(200);
    }
}
