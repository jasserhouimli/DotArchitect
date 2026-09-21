using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace DotArchitect.Modules.Identity.Features.Register;

public class RegisterHandler(UserManager<User> userManager)
{
    public async Task<Result<RegisterResponse>> Handle(
        RegisterRequest request,
        CancellationToken ct)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
            return Result<RegisterResponse>.Failure("Email already registered", 409);

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result<RegisterResponse>.Failure(errors, 400);
        }

        return Result<RegisterResponse>.Success(new RegisterResponse(
            Guid.Parse(user.Id),
            user.Email!,
            user.FullName), 201);
    }
}

public record RegisterResponse(
    Guid Id,
    string Email,
    string FullName
);
