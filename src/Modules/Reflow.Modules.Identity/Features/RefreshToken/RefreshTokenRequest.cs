namespace Reflow.Modules.Identity.Features.RefreshToken;

public record RefreshTokenRequest(
    string RefreshToken
);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken
);
