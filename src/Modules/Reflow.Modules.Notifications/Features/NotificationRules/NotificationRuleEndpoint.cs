using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.Notifications.Features.NotificationRules;

public static class NotificationRuleEndpoint
{
    private static Guid UserId(HttpContext http) =>
        Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}/notifications/rule", async (
            Guid workflowId, NotificationRuleHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var rule = await handler.GetAsync(workflowId, UserId(http), ct);
            if (rule is null) return Results.NotFound(new { error = "Rule not found" });
            return Results.Ok(rule);
        }).RequireAuthorization().WithName("GetNotificationRule").WithTags("Notifications");

        app.MapPut("/api/v1/workflows/{workflowId:guid}/notifications/rule", async (
            Guid workflowId, UpsertRuleRequest request, NotificationRuleHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.UpsertAsync(workflowId, request, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(result.Value);
        }).RequireAuthorization().WithName("UpsertNotificationRule").WithTags("Notifications");

        app.MapDelete("/api/v1/workflows/{workflowId:guid}/notifications/rule", async (
            Guid workflowId, NotificationRuleHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.DeleteAsync(workflowId, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Rule deleted" });
        }).RequireAuthorization().WithName("DeleteNotificationRule").WithTags("Notifications");
    }
}
