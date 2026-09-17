namespace FieldOps.Modules.Technicians.Features.UpdateTechnician;

public record UpdateTechnicianRequest(
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    bool IsActive
);
