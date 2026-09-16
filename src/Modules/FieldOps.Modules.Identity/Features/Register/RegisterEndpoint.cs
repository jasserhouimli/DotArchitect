using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FieldOps.Modules.Identity.Persistence;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/register", async (
            RegisterRequest request,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            return await RegisterHandler.Handle(request, db, ct);
        })
        .WithName("Register")
        .Produces(201)
        .Produces(409);
    }
}
