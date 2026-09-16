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
            LoginRequest request,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration config,
            CancellationToken ct) =>
        {
            return await LoginHandler.Handle(request, userManager, signInManager, config, ct);
        })
        .WithName("Login")
        .Produces(200)
        .Produces(401);
    }
}
