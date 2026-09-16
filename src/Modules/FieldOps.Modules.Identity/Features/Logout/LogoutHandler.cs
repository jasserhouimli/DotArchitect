using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Identity.Features.Logout;

public static class LogoutHandler
{
    public static async Task<IResult> Handle(
        LogoutRequest request,
        TokenService tokenService,
        HttpContext http,
        CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        http.Response.Cookies.Delete("FieldOps.Token");

        return Results.Ok(new { message = "Logged out successfully" });
    }
}
