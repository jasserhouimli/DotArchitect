using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.Login;

public static class LoginHandler
{
    public static async Task<LoginResponse?> Handle(
        LoginRequest request,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        TokenService tokenService,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return null;

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
            return null;

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(Guid.Parse(user.Id), ct);

        return new LoginResponse(
            accessToken,
            refreshToken.Token,
            Guid.Parse(user.Id),
            user.Email!,
            user.FullName);
    }
}
