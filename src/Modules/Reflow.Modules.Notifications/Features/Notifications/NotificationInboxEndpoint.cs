using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.Notifications.Features.Notifications;

public static class NotificationInboxEndpoint
{
    private static Guid UserId(HttpContext http) =>
        Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/notifications", async (
            bool? unreadOnly, int? take, NotificationInboxHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var items = await handler.ListAsync(UserId(http), unreadOnly == true, take ?? 50, ct);
            return Results.Ok(items);
        }).RequireAuthorization().WithName("ListNotifications").WithTags("Notifications");

        app.MapGet("/api/v1/notifications/unread-count", async (
            NotificationInboxHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var count = await handler.UnreadCountAsync(UserId(http), ct);
            return Results.Ok(new { unread = count });
        }).RequireAuthorization().WithName("UnreadNotificationCount").WithTags("Notifications");

        app.MapPost("/api/v1/notifications/{notificationId:guid}/read", async (
            Guid notificationId, NotificationInboxHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.MarkReadAsync(notificationId, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Marked as read" });
        }).RequireAuthorization().WithName("MarkNotificationRead").WithTags("Notifications");

        app.MapPost("/api/v1/notifications/read-all", async (
            NotificationInboxHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var count = await handler.MarkAllReadAsync(UserId(http), ct);
            return Results.Ok(new { marked = count });
        }).RequireAuthorization().WithName("MarkAllNotificationsRead").WithTags("Notifications");
    }
}
