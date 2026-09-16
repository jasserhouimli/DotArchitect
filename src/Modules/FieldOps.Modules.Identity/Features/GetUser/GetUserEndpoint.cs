using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using FieldOps.Modules.Identity.Domain;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/users/{id:guid}", async (
            Guid id,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            return await GetUserHandler.Handle(new GetUserQuery(id), userManager, ct);
        })
        .WithName("GetUser")
        .Produces(200)
        .Produces(404);
    }
}
