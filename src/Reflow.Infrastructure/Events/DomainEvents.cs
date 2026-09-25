namespace Reflow.Infrastructure.Events;

public record RunEventData(
    Guid Id,
    Guid WorkflowId,
    int VersionNumber,
    int Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error,
    int TotalTasks,
    int CompletedTasks,
    int FailedTasks);

public record TaskEventData(
    Guid Id,
    Guid WorkflowRunId,
    string NodeId,
    string NodeType,
    int Status,
    string? Error,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int AttemptCount,
    string? OutputSummary,
    int? RowCount);

public record LogEventData(
    Guid Id,
    Guid? TaskRunId,
    string Message,
    string Level,
    DateTime Timestamp);

public record RunFinishedEvent(
    Guid RunId,
    Guid WorkflowId,
    Guid OwnerId,
    string WorkflowName,
    int VersionNumber,
    bool Succeeded,
    int TotalTasks,
    int CompletedTasks,
    int FailedTasks,
    int TotalRejected,
    string? Error,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record TaskChangedEvent(Guid RunId, TaskEventData Task);

public record LogRecordedEvent(Guid RunId, LogEventData Log);

public record RunProgressEvent(RunEventData Run);

public record WorkflowDeletedEvent(Guid WorkflowId, Guid OwnerId);
