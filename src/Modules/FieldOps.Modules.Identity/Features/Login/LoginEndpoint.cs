using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using FieldOps.Modules.Identity.Persistence;

namespace FieldOps.Modules.Identity.Features.Login;

public static class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            IdentityDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            return await LoginHandler.Handle(request, db, config, ct);
        })
        .WithName("Login")
        .Produces(200)
        .Produces(401);
    }
}
