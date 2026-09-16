using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenHandler
{
    public static async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenRequest request,
        UserManager<User> userManager,
        TokenService tokenService,
        CancellationToken ct)
    {
        var storedToken = await tokenService.ValidateRefreshTokenAsync(request.RefreshToken, ct);

        if (storedToken is null)
            return Result<RefreshTokenResponse>.Failure("Invalid or expired refresh token", 401);

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());

        if (user is null)
            return Result<RefreshTokenResponse>.Failure("User not found", 401);

        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        var newAccessToken = tokenService.GenerateAccessToken(user);
        var newRefreshToken = await tokenService.GenerateRefreshTokenAsync(storedToken.UserId, ct);

        return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(newAccessToken, newRefreshToken.Token));
    }
}
