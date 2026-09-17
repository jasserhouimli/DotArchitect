using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Domain;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.CreateTechnician;

public class CreateTechnicianHandler(TechniciansDbContext db)
{
    public async Task<Result<CreateTechnicianResponse>> Handle(
        CreateTechnicianRequest request,
        CancellationToken ct)
    {
        var existingEmail = await db.Technicians
            .AnyAsync(t => t.Email == request.Email, ct);

        if (existingEmail)
            return Result<CreateTechnicianResponse>.Failure("Email already registered", 409);

        var technician = new Technician
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            HourlyRate = request.HourlyRate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);

        return Result<CreateTechnicianResponse>.Success(new CreateTechnicianResponse(
            technician.Id,
            technician.UserId,
            technician.FirstName,
            technician.LastName,
            technician.Email,
            technician.Phone,
            technician.HourlyRate,
            technician.IsActive,
            technician.CreatedAt), 201);
    }
}

public record CreateTechnicianResponse(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    bool IsActive,
    DateTime CreatedAt
);
