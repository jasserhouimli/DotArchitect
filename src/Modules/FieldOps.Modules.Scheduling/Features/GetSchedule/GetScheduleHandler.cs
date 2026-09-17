using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Scheduling.Domain;
using FieldOps.Modules.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Scheduling.Features.GetSchedule;

public class GetScheduleHandler(SchedulingDbContext db)
{
    public async Task<Result<GetScheduleResponse>> Handle(
        GetScheduleQuery query,
        CancellationToken ct)
    {
        var schedule = await db.Schedules
            .Where(s => s.Id == query.Id)
            .Select(s => new GetScheduleResponse(
                s.Id, s.TechnicianId, s.WorkOrderId,
                s.Title, s.Description, s.StartTime,
                s.EndTime, s.Status, s.Notes,
                s.CreatedAt, s.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (schedule is null)
            return Result<GetScheduleResponse>.Failure("Schedule not found", 404);

        return Result<GetScheduleResponse>.Success(schedule);
    }
}

public record GetScheduleResponse(
    Guid Id, Guid TechnicianId, Guid? WorkOrderId,
    string Title, string? Description, DateTime StartTime,
    DateTime EndTime, ScheduleStatus Status, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt
);
