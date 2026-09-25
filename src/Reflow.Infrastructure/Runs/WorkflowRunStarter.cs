using Reflow.Infrastructure.Results;

namespace Reflow.Infrastructure.Runs;

public record RunTrigger(string Kind, string? Name, string? PayloadJson);

public interface IWorkflowRunStarter
{
    Task<Result<Guid>> StartRunAsync(Guid workflowId, Guid ownerId, RunTrigger? trigger, CancellationToken ct);
}

public interface IRunMonitor
{
    Task<int> CountActiveRunsAsync(Guid workflowId, Guid ownerId, CancellationToken ct);
}
