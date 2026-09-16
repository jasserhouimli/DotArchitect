using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using FieldOps.Modules.Identity.Domain;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/register", async (
            RegisterRequest request,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            return await RegisterHandler.Handle(request, userManager, ct);
        })
        .WithName("Register")
        .Produces(201)
        .Produces(409);
    }
}
