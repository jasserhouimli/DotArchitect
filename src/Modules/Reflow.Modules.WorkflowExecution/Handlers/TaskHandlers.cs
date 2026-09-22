namespace Reflow.Modules.WorkflowExecution.Handlers;

public record TaskExecutionContext(Guid WorkflowRunId, Guid TaskRunId, string NodeId, string NodeType, string? ConfigJson);

public record TaskExecutionResult(bool IsSuccess, string? Error, string? Output, string? Log);

public interface ITaskHandler
{
    string TaskType { get; }
    Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken ct);
}

public class TaskHandlerRegistry
{
    private readonly Dictionary<string, ITaskHandler> _handlers;
    public TaskHandlerRegistry(IEnumerable<ITaskHandler> handlers) => _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);
    public bool TryGet(string taskType, out ITaskHandler? handler) => _handlers.TryGetValue(taskType, out handler);
}

// Stub handlers — deterministic, no external I/O yet

public class CsvReadHandler : ITaskHandler
{
    public string TaskType => "data.csv.read";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "csv:10 rows", "CSV read simulated"));
}

public class ValidateHandler : ITaskHandler
{
    public string TaskType => "data.validate";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "validated", "Validation passed"));
}

public class FilterHandler : ITaskHandler
{
    public string TaskType => "data.filter";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "filtered", "Filter applied"));
}

public class TransformHandler : ITaskHandler
{
    public string TaskType => "data.transform";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "transformed", "Transform done"));
}

public class AggregateHandler : ITaskHandler
{
    public string TaskType => "data.aggregate";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "aggregated", "Aggregate done"));
}

public class OutputHandler : ITaskHandler
{
    public string TaskType => "data.output";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "output persisted", "Output saved"));
}

public class HttpRequestHandler : ITaskHandler
{
    public string TaskType => "http.request";
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(new TaskExecutionResult(true, null, "http:200", "HTTP GET simulated"));
}
