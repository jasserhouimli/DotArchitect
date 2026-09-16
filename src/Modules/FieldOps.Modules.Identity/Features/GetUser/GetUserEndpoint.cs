using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FieldOps.Modules.Identity.Persistence;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/users/{id:guid}", async (
            Guid id,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            return await GetUserHandler.Handle(new GetUserQuery(id), db, ct);
        })
        .WithName("GetUser")
        .Produces(200)
        .Produces(404);
    }
}
