using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Scheduling.Domain;
using FieldOps.Modules.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Scheduling.Features.UpdateSchedule;

public class UpdateScheduleHandler(SchedulingDbContext db)
{
    public async Task<Result<UpdateScheduleResponse>> Handle(
        Guid id,
        UpdateScheduleRequest request,
        CancellationToken ct)
    {
        var schedule = await db.Schedules.FindAsync([id], ct);

        if (schedule is null)
            return Result<UpdateScheduleResponse>.Failure("Schedule not found", 404);

        if (request.Title is not null)
            schedule.Title = request.Title;
        if (request.Description is not null)
            schedule.Description = request.Description;
        if (request.StartTime.HasValue)
            schedule.StartTime = request.StartTime.Value;
        if (request.EndTime.HasValue)
            schedule.EndTime = request.EndTime.Value;
        if (request.Status.HasValue)
            schedule.Status = request.Status.Value;
        if (request.Notes is not null)
            schedule.Notes = request.Notes;

        schedule.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateScheduleResponse>.Success(new UpdateScheduleResponse(
            schedule.Id, schedule.TechnicianId, schedule.WorkOrderId,
            schedule.Title, schedule.Description, schedule.StartTime,
            schedule.EndTime, schedule.Status, schedule.Notes,
            schedule.CreatedAt, schedule.UpdatedAt));
    }
}

public record UpdateScheduleResponse(
    Guid Id, Guid TechnicianId, Guid? WorkOrderId,
    string Title, string? Description, DateTime StartTime,
    DateTime EndTime, ScheduleStatus Status, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt
);
