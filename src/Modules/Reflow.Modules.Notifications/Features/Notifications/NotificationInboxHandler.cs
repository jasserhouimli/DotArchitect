using Reflow.Infrastructure.Results;
using Reflow.Modules.Notifications.Domain;
using Reflow.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Notifications.Features.Notifications;

public class NotificationInboxHandler(NotificationsDbContext db)
{
    public async Task<List<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        var query = db.Notifications.AsNoTracking().Where(n => n.OwnerId == userId);
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.WorkflowId, n.WorkflowRunId, (int)n.Kind,
                n.Title, n.Message, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
    {
        return await db.Notifications.CountAsync(n => n.OwnerId == userId && !n.IsRead, ct);
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.OwnerId == userId, ct);
        if (notification is null) return Result.Failure("Notification not found", 404);

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Result.Success(200);
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var unread = await db.Notifications
            .Where(n => n.OwnerId == userId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unread)
            n.IsRead = true;
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }
}

public record NotificationDto(Guid Id, Guid WorkflowId, Guid WorkflowRunId, int Kind,
    string Title, string Message, bool IsRead, DateTime CreatedAt);
