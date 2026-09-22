namespace Reflow.Modules.Identity.Features.Register;

public record RegisterRequest(
    string Email,
    string DisplayName,
    string Password
);
