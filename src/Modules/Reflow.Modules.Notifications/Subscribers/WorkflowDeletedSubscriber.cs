using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reflow.Infrastructure.Events;
using Reflow.Modules.Notifications.Persistence;

namespace Reflow.Modules.Notifications.Subscribers;

public class WorkflowDeletedSubscriber(
    NotificationsDbContext db,
    ILogger<WorkflowDeletedSubscriber> logger)
    : IEventHandler<WorkflowDeletedEvent>
{
    public async Task HandleAsync(WorkflowDeletedEvent @event, CancellationToken ct)
    {
        try
        {
            var rules = await db.NotificationRules
                .Where(r => r.WorkflowId == @event.WorkflowId && r.OwnerId == @event.OwnerId)
                .ToListAsync(ct);
            if (rules.Count == 0) return;

            db.NotificationRules.RemoveRange(rules);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up notification rules for workflow {WorkflowId}", @event.WorkflowId);
        }
    }
}
