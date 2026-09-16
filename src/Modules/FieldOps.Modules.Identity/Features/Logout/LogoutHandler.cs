using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Identity.Features.Logout;

public static class LogoutHandler
{
    public static async Task<Result<LogoutResponse>> Handle(
        LogoutRequest request,
        TokenService tokenService,
        HttpContext http,
        CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        http.Response.Cookies.Delete("FieldOps.Token");

        return Result<LogoutResponse>.Success(new LogoutResponse("Logged out successfully"));
    }
}

public record LogoutResponse(string Message);
