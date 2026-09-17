namespace FieldOps.Modules.Identity.Features.Register;

public record RegisterRequest(
    string Email,
    string FullName,
    string Password
);
