using Microsoft.AspNetCore.SignalR;
using Reflow.Infrastructure.Events;

namespace Reflow.Infrastructure.Realtime;

public class RealtimeBridge(IHubContext<RunHub> hub)
    : IEventHandler<TaskChangedEvent>,
      IEventHandler<LogRecordedEvent>,
      IEventHandler<RunProgressEvent>,
      IEventHandler<RunFinishedEvent>
{
    public Task HandleAsync(TaskChangedEvent @event, CancellationToken ct)
        => hub.Clients.Group(RunHub.GroupFor(@event.RunId)).SendAsync("taskUpdated", @event.Task, ct);

    public Task HandleAsync(LogRecordedEvent @event, CancellationToken ct)
        => hub.Clients.Group(RunHub.GroupFor(@event.RunId)).SendAsync("logAppended", @event.Log, ct);

    public Task HandleAsync(RunProgressEvent @event, CancellationToken ct)
        => hub.Clients.Group(RunHub.GroupFor(@event.Run.Id)).SendAsync("runUpdated", @event.Run, ct);

    public Task HandleAsync(RunFinishedEvent @event, CancellationToken ct)
        => hub.Clients.Group(RunHub.GroupFor(@event.RunId)).SendAsync("runUpdated",
            new
            {
                id = @event.RunId,
                workflowId = @event.WorkflowId,
                versionNumber = @event.VersionNumber,
                status = @event.Succeeded ? 2 : 3,
                createdAt = @event.CreatedAt,
                startedAt = @event.StartedAt,
                completedAt = @event.CompletedAt,
                error = @event.Error,
                totalTasks = @event.TotalTasks,
                completedTasks = @event.CompletedTasks,
                failedTasks = @event.FailedTasks
            }, ct);
}
