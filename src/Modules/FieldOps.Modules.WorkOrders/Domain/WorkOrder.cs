namespace FieldOps.Modules.WorkOrders.Domain;

public class WorkOrder
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Pending;
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum WorkOrderStatus
{
    Pending = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum WorkOrderPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}
