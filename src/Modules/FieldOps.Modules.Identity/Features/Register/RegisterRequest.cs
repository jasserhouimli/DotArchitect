namespace FieldOps.Modules.Identity.Features.Register;

public record RegisterRequest(
    string Email,
    string FullName,
    string Password
);

public record RegisterResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Email,
    string FullName
);
