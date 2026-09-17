using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Domain;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.AddTechnicianSkill;

public class AddTechnicianSkillHandler(TechniciansDbContext db)
{
    public async Task<Result<AddTechnicianSkillResponse>> Handle(
        Guid technicianId,
        AddTechnicianSkillRequest request,
        CancellationToken ct)
    {
        var technician = await db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, ct);

        if (technician is null)
            return Result<AddTechnicianSkillResponse>.Failure("Technician not found", 404);

        var existingSkill = await db.TechnicianSkills
            .AnyAsync(s => s.TechnicianId == technicianId && s.SkillName == request.SkillName, ct);

        if (existingSkill)
            return Result<AddTechnicianSkillResponse>.Failure("Skill already exists for this technician", 409);

        var skill = new TechnicianSkill
        {
            Id = Guid.NewGuid(),
            TechnicianId = technicianId,
            SkillName = request.SkillName,
            ProficiencyLevel = request.ProficiencyLevel
        };

        db.TechnicianSkills.Add(skill);
        await db.SaveChangesAsync(ct);

        return Result<AddTechnicianSkillResponse>.Success(new AddTechnicianSkillResponse(
            skill.Id,
            skill.SkillName,
            skill.ProficiencyLevel), 201);
    }
}

public record AddTechnicianSkillResponse(
    Guid Id,
    string SkillName,
    int ProficiencyLevel
);
