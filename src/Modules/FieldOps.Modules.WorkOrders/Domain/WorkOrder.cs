namespace FieldOps.Modules.WorkOrders.Domain;

public class WorkOrder
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
