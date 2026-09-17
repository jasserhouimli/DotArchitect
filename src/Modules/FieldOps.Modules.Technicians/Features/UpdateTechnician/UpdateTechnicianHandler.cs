using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Domain;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.UpdateTechnician;

public class UpdateTechnicianHandler(TechniciansDbContext db)
{
    public async Task<Result<UpdateTechnicianResponse>> Handle(
        Guid id,
        UpdateTechnicianRequest request,
        CancellationToken ct)
    {
        var technician = await db.Technicians.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (technician is null)
            return Result<UpdateTechnicianResponse>.Failure("Technician not found", 404);

        var emailTaken = await db.Technicians
            .AnyAsync(t => t.Email == request.Email && t.Id != id, ct);

        if (emailTaken)
            return Result<UpdateTechnicianResponse>.Failure("Email already in use", 409);

        technician.UserId = request.UserId;
        technician.FirstName = request.FirstName;
        technician.LastName = request.LastName;
        technician.Email = request.Email;
        technician.Phone = request.Phone;
        technician.HourlyRate = request.HourlyRate;
        technician.IsActive = request.IsActive;
        technician.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateTechnicianResponse>.Success(new UpdateTechnicianResponse(
            technician.Id,
            technician.UserId,
            technician.FirstName,
            technician.LastName,
            technician.Email,
            technician.Phone,
            technician.HourlyRate,
            technician.IsActive,
            technician.CreatedAt,
            technician.UpdatedAt));
    }
}

public record UpdateTechnicianResponse(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
