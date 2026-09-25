namespace Reflow.Modules.WorkflowExecution.Domain;

public enum WorkflowRunStatus { Queued = 0, Running = 1, Completed = 2, Failed = 3, Cancelled = 4 }
public enum TaskRunStatus { Pending = 0, Ready = 1, Running = 2, Completed = 3, Failed = 4, RetryScheduled = 5, Cancelled = 6, Skipped = 7 }

public class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public int VersionNumber { get; set; }
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Queued;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? EdgesJson { get; set; }
    public string TriggerKind { get; set; } = "manual";
    public string? TriggerName { get; set; }
    public string? TriggerPayloadJson { get; set; }
}

public class TaskRun
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public TaskRunStatus Status { get; set; } = TaskRunStatus.Pending;
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ConfigJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime? NotBefore { get; set; }
    public uint RowVersion { get; set; }
}

public class TaskAttempt
{
    public Guid Id { get; set; }
    public Guid TaskRunId { get; set; }
    public int AttemptNumber { get; set; }
    public TaskRunStatus Status { get; set; } = TaskRunStatus.Running;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? Log { get; set; }
}

public class ExecutionLog
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid? TaskRunId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
