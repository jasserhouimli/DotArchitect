using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FieldOps.Modules.Identity.Services;

namespace FieldOps.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/logout", async (
            HttpContext http,
            LogoutRequest request,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            return await LogoutHandler.Handle(request, tokenService, http, ct);
        })
        .WithName("Logout")
        .Produces(200);
    }
}
