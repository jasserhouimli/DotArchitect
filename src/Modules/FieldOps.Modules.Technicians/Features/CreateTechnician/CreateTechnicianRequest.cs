namespace FieldOps.Modules.Technicians.Features.CreateTechnician;

public record CreateTechnicianRequest(
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate
);
