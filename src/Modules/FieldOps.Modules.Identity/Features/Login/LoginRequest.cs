namespace FieldOps.Modules.Identity.Features.Login;

public record LoginRequest(
    string Email,
    string Password
);

public record LoginResponse(
    string Token,
    Guid Id,
    string Email,
    string FullName
);
