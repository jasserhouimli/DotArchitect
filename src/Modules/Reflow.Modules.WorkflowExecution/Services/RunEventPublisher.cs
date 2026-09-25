using Microsoft.AspNetCore.SignalR;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Features.GetTaskRuns;
using Reflow.Modules.WorkflowExecution.Features.GetWorkflowRun;
using Reflow.Modules.WorkflowExecution.Hubs;

namespace Reflow.Modules.WorkflowExecution.Services;

public class RunEventPublisher(
    IHubContext<RunHub> hub,
    GetWorkflowRunHandler runQueries,
    GetTaskRunsHandler taskQueries)
{
    public async Task RunUpdated(Guid runId, Guid userId, CancellationToken ct)
    {
        var run = await runQueries.Handle(runId, userId, ct);
        if (run is null) return;
        await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runUpdated", run, ct);
    }

    public async Task TaskUpdated(Guid runId, Guid taskId, Guid userId, CancellationToken ct)
    {
        var tasks = await taskQueries.Handle(runId, userId, ct);
        var task = tasks?.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;
        await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("taskUpdated", task, ct);
    }

    public async Task LogAppended(Guid runId, Guid logId, Guid? taskRunId, string message, string level, DateTime timestamp, CancellationToken ct)
    {
        await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("logAppended",
            new { id = logId, taskRunId, message, level, timestamp }, ct);
    }
}
