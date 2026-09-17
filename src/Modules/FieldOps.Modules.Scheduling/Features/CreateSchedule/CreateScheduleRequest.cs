namespace FieldOps.Modules.Scheduling.Features.CreateSchedule;

public record CreateScheduleRequest(
    Guid TechnicianId,
    Guid? WorkOrderId,
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes
);
