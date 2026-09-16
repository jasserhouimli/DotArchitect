using FieldOps.Modules.Identity.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserHandler
{
    public static async Task<IResult> Handle(
        GetUserQuery query,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(query.Id.ToString());

        if (user is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.CreatedAt
        });
    }
}
