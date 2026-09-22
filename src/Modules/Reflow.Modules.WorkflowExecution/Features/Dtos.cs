namespace Reflow.Modules.WorkflowExecution.Features;

public record RunDto(
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

public record TaskDto(
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

public record TaskDetailDto(
    Guid Id,
    Guid WorkflowRunId,
    string NodeId,
    string NodeType,
    int Status,
    string? Error,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ConfigJson,
    string? OutputJson);

public record AttemptDto(
    Guid Id,
    int AttemptNumber,
    int Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Error,
    string? Log);
