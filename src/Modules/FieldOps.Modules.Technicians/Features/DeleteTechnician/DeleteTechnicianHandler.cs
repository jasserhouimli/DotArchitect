using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.DeleteTechnician;

public class DeleteTechnicianHandler(TechniciansDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var technician = await db.Technicians.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (technician is null)
            return Result.Failure("Technician not found", 404);

        db.Technicians.Remove(technician);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
