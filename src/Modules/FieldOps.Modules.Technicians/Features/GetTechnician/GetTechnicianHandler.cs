using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.GetTechnician;

public class GetTechnicianHandler(TechniciansDbContext db)
{
    public async Task<Result<GetTechnicianResponse>> Handle(
        GetTechnicianQuery query,
        CancellationToken ct)
    {
        var technician = await db.Technicians
            .Include(t => t.Skills)
            .FirstOrDefaultAsync(t => t.Id == query.Id, ct);

        if (technician is null)
            return Result<GetTechnicianResponse>.Failure("Technician not found", 404);

        return Result<GetTechnicianResponse>.Success(new GetTechnicianResponse(
            technician.Id,
            technician.UserId,
            technician.FirstName,
            technician.LastName,
            technician.Email,
            technician.Phone,
            technician.HourlyRate,
            technician.IsActive,
            technician.CreatedAt,
            technician.UpdatedAt,
            technician.Skills.Select(s => new TechnicianSkillDto(
                s.Id,
                s.SkillName,
                s.ProficiencyLevel)).ToList()));
    }
}

public record GetTechnicianResponse(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<TechnicianSkillDto> Skills
);

public record TechnicianSkillDto(
    Guid Id,
    string SkillName,
    int ProficiencyLevel
);
