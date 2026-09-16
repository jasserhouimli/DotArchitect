using Microsoft.AspNetCore.Http;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterHandler
{
    public static async Task<IResult> Handle(
        RegisterRequest request,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);

        if (exists)
            return Results.Conflict(new { error = "Email already registered" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Technician",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/users/{user.Id}", new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Role
        });
    }
}
