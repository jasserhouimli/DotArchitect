using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterHandler
{
    public static async Task<Result<RegisterResponse>> Handle(
        RegisterRequest request,
        UserManager<User> userManager,
        TokenService tokenService,
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

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(Guid.Parse(user.Id), ct);

        return Result<RegisterResponse>.Success(new RegisterResponse(
            accessToken,
            refreshToken.Token,
            Guid.Parse(user.Id),
            user.Email!,
            user.FullName), 201);
    }
}
