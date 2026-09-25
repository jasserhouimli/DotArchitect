using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reflow.Infrastructure.Events;
using Reflow.Infrastructure.Realtime;
using Reflow.Modules.Notifications.Domain;
using Reflow.Modules.Notifications.Persistence;
using Reflow.Modules.Notifications.Services;

namespace Reflow.Modules.Notifications.Subscribers;

public class RunFinishedSubscriber(
    NotificationsDbContext db,
    WebhookSender webhooks,
    IHubContext<RunHub> hub,
    ILogger<RunFinishedSubscriber> logger)
    : IEventHandler<RunFinishedEvent>
{
    public async Task HandleAsync(RunFinishedEvent @event, CancellationToken ct)
    {
        try
        {
            await HandleCoreAsync(@event, ct);
        }
        catch (Exception ex)
        {
            // Notifications must never break whoever published the event.
            logger.LogWarning(ex, "Failed to process run finished event for run {RunId}", @event.RunId);
        }
    }

    private async Task HandleCoreAsync(RunFinishedEvent @event, CancellationToken ct)
    {
        var rule = await db.NotificationRules
            .FirstOrDefaultAsync(x => x.WorkflowId == @event.WorkflowId && x.OwnerId == @event.OwnerId, ct);
        if (rule is null) return;

        var events = new List<(NotificationKind Kind, string Title, string Message, string Event)>();
        var shortId = @event.RunId.ToString("N")[..8];
        if (!@event.Succeeded && rule.NotifyOnFailure)
        {
            events.Add((NotificationKind.RunFailed,
                $"Run failed: {@event.WorkflowName} v{@event.VersionNumber}",
                $"Run {shortId} failed: {Trim(@event.Error, 300)}",
                "run.failed"));
        }
        if (@event.Succeeded && rule.NotifyOnSuccess)
        {
            events.Add((NotificationKind.RunSucceeded,
                $"Run succeeded: {@event.WorkflowName} v{@event.VersionNumber}",
                $"Run {shortId} completed: {@event.CompletedTasks}/{@event.TotalTasks} tasks succeeded, {@event.TotalRejected} rows rejected.",
                "run.succeeded"));
        }
        if (rule.RejectsAbove.HasValue && @event.TotalRejected > rule.RejectsAbove.Value)
        {
            events.Add((NotificationKind.RejectsAboveThreshold,
                $"Quality threshold breached: {@event.WorkflowName} v{@event.VersionNumber}",
                $"Run {shortId} rejected {@event.TotalRejected} rows (threshold {rule.RejectsAbove.Value}).",
                "run.rejectsAboveThreshold"));
        }
        if (events.Count == 0) return;

        foreach (var (kind, title, message, eventName) in events)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                WorkflowId = @event.WorkflowId,
                WorkflowRunId = @event.RunId,
                OwnerId = @event.OwnerId,
                Kind = kind,
                Title = title,
                Message = message
            });

            if (!string.IsNullOrWhiteSpace(rule.WebhookUrl))
            {
                var payload = WebhookPayload.Build(rule.WebhookUrl, eventName, title, message,
                    @event.WorkflowId, @event.WorkflowName, @event.RunId, @event.VersionNumber,
                    @event.Succeeded, @event.TotalTasks, @event.CompletedTasks, @event.FailedTasks,
                    @event.TotalRejected, @event.Error);
                await webhooks.SendAsync(rule.WebhookUrl, payload, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        var saved = db.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Unchanged)
            .Select(e => e.Entity)
            .ToList();
        foreach (var n in saved)
        {
            await hub.Clients.Group(RunHub.UserGroup(n.OwnerId)).SendAsync("notificationReceived",
                new { id = n.Id, workflowId = n.WorkflowId, workflowRunId = n.WorkflowRunId, kind = (int)n.Kind, title = n.Title, message = n.Message, isRead = n.IsRead, createdAt = n.CreatedAt }, ct);
        }

        static string Trim(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var oneLine = value.Replace('\r', ' ').Replace('\n', ' ');
            return oneLine.Length > max ? oneLine[..max] : oneLine;
        }
    }
}
