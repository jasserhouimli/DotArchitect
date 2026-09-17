using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Identity.Features.Logout;

public class LogoutHandler(TokenService tokenService)
{
    public async Task<Result<LogoutResponse>> Handle(
        LogoutRequest request,
        HttpContext http,
        CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        http.Response.Cookies.Delete("FieldOps.Token");

        return Result<LogoutResponse>.Success(new LogoutResponse("Logged out successfully"));
    }
}

public record LogoutResponse(string Message);
