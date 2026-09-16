using FieldOps.Modules.Identity.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterHandler
{
    public static async Task<IResult> Handle(
        RegisterRequest request,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
            return Results.Conflict(new { error = "Email already registered" });

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Results.Created($"/users/{user.Id}", new
        {
            user.Id,
            user.Email,
            user.FullName
        });
    }
}
