namespace Reflow.Modules.Notifications.Domain;

public class NotificationRule
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid OwnerId { get; set; }
    public bool NotifyOnSuccess { get; set; }
    public bool NotifyOnFailure { get; set; }
    public int? RejectsAbove { get; set; }
    public string? WebhookUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationKind { RunSucceeded = 0, RunFailed = 1, RejectsAboveThreshold = 2 }

public class Notification
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid OwnerId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
