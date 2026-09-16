namespace FieldOps.Modules.Identity.Features.Login;

public record LoginRequest(
    string Email,
    string Password
);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Email,
    string FullName
);
