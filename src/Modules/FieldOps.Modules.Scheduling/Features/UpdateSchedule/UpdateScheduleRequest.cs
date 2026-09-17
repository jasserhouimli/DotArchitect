using FieldOps.Modules.Scheduling.Domain;

namespace FieldOps.Modules.Scheduling.Features.UpdateSchedule;

public record UpdateScheduleRequest(
    string? Title,
    string? Description,
    DateTime? StartTime,
    DateTime? EndTime,
    ScheduleStatus? Status,
    string? Notes
);
