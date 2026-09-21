using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Identity.Domain;
using DotArchitect.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace DotArchitect.Modules.Identity.Features.Login;

public class LoginHandler(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    TokenService tokenService)
{
    public async Task<Result<LoginResponse>> Handle(
        LoginRequest request,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return Result<LoginResponse>.Failure("Invalid email or password", 401);

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
            return Result<LoginResponse>.Failure("Invalid email or password", 401);

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(Guid.Parse(user.Id), ct);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshToken.Token,
            Guid.Parse(user.Id),
            user.Email!,
            user.FullName));
    }
}
