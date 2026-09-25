namespace Reflow.Modules.Triggers.Domain;

public enum TriggerKind { Schedule = 0, Webhook = 1 }

public enum OverlapPolicy { Skip = 0, Queue = 1 }

public class Trigger
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid OwnerId { get; set; }
    public TriggerKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? CronExpression { get; set; }
    public string? Timezone { get; set; }
    public OverlapPolicy OverlapPolicy { get; set; } = OverlapPolicy.Skip;
    public string? SecretTokenHash { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastFiredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
