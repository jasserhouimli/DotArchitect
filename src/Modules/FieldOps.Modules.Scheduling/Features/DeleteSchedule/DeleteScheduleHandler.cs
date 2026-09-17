using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Scheduling.Features.DeleteSchedule;

public class DeleteScheduleHandler(SchedulingDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var schedule = await db.Schedules.FindAsync([id], ct);

        if (schedule is null)
            return Result.Failure("Schedule not found", 404);

        db.Schedules.Remove(schedule);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
