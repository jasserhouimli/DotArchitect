using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenHandler
{
    public static async Task<RefreshTokenResponse?> Handle(
        RefreshTokenRequest request,
        UserManager<User> userManager,
        TokenService tokenService,
        CancellationToken ct)
    {
        var storedToken = await tokenService.ValidateRefreshTokenAsync(request.RefreshToken, ct);

        if (storedToken is null)
            return null;

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());

        if (user is null)
            return null;

        // Revoke old refresh token
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        // Generate new tokens
        var newAccessToken = tokenService.GenerateAccessToken(user);
        var newRefreshToken = await tokenService.GenerateRefreshTokenAsync(storedToken.UserId, ct);

        return new RefreshTokenResponse(newAccessToken, newRefreshToken.Token);
    }
}
