using FieldOps.Modules.Scheduling.Domain;

namespace FieldOps.Modules.Scheduling.Features.GetSchedules;

public record GetSchedulesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? TechnicianId = null,
    ScheduleStatus? Status = null,
    DateTime? From = null,
    DateTime? To = null
);
