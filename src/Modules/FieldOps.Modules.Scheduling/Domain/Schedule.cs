namespace FieldOps.Modules.Scheduling.Domain;

public class Schedule
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Scheduled;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ScheduleStatus
{
    Scheduled = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}
