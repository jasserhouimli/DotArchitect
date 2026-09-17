using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Scheduling.Domain;
using FieldOps.Modules.Scheduling.Persistence;

namespace FieldOps.Modules.Scheduling.Features.CreateSchedule;

public class CreateScheduleHandler(SchedulingDbContext db)
{
    public async Task<Result<CreateScheduleResponse>> Handle(
        CreateScheduleRequest request,
        CancellationToken ct)
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TechnicianId = request.TechnicianId,
            WorkOrderId = request.WorkOrderId,
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Notes = request.Notes,
            Status = ScheduleStatus.Scheduled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(ct);

        return Result<CreateScheduleResponse>.Success(new CreateScheduleResponse(
            schedule.Id, schedule.TechnicianId, schedule.WorkOrderId,
            schedule.Title, schedule.Description, schedule.StartTime,
            schedule.EndTime, schedule.Status, schedule.Notes,
            schedule.CreatedAt), 201);
    }
}

public record CreateScheduleResponse(
    Guid Id, Guid TechnicianId, Guid? WorkOrderId,
    string Title, string? Description, DateTime StartTime,
    DateTime EndTime, ScheduleStatus Status, string? Notes,
    DateTime CreatedAt
);
