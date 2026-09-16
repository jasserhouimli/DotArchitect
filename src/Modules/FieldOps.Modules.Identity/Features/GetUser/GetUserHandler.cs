using FieldOps.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserHandler
{
    public static async Task<IResult> Handle(
        GetUserQuery query,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users
            .Where(u => u.Id == query.Id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return Results.NotFound();

        return Results.Ok(user);
    }
}
