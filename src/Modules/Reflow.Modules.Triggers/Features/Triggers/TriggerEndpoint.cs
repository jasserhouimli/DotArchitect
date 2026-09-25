using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.Triggers.Features.Triggers;

public static class TriggerEndpoint
{
    private static Guid UserId(HttpContext http) =>
        Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string BaseUrl(HttpContext http) =>
        $"{http.Request.Scheme}://{http.Request.Host}";

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows/{workflowId:guid}/triggers", async (
            Guid workflowId, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var items = await handler.ListAsync(workflowId, UserId(http), ct);
            if (items is null) return Results.NotFound(new { error = "Workflow not found" });
            return Results.Ok(items);
        }).RequireAuthorization().WithName("ListTriggers").WithTags("Triggers");

        app.MapPost("/api/v1/workflows/{workflowId:guid}/triggers/schedules", async (
            Guid workflowId, CreateScheduleRequest request, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.CreateScheduleAsync(workflowId, request, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Json(result.Value, statusCode: 201);
        }).RequireAuthorization().WithName("CreateScheduleTrigger").WithTags("Triggers");

        app.MapPut("/api/v1/workflows/{workflowId:guid}/triggers/schedules/{triggerId:guid}", async (
            Guid workflowId, Guid triggerId, UpdateScheduleRequest request, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.UpdateScheduleAsync(workflowId, triggerId, request, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(result.Value);
        }).RequireAuthorization().WithName("UpdateScheduleTrigger").WithTags("Triggers");

        app.MapPost("/api/v1/workflows/{workflowId:guid}/triggers/webhooks", async (
            Guid workflowId, CreateWebhookRequest request, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.CreateWebhookAsync(workflowId, request, BaseUrl(http), UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Json(result.Value, statusCode: 201);
        }).RequireAuthorization().WithName("CreateWebhookTrigger").WithTags("Triggers");

        app.MapPost("/api/v1/workflows/{workflowId:guid}/triggers/webhooks/{triggerId:guid}/regenerate", async (
            Guid workflowId, Guid triggerId, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.RegenerateWebhookAsync(workflowId, triggerId, BaseUrl(http), UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(result.Value);
        }).RequireAuthorization().WithName("RegenerateWebhookTrigger").WithTags("Triggers");

        app.MapDelete("/api/v1/workflows/{workflowId:guid}/triggers/{triggerId:guid}", async (
            Guid workflowId, Guid triggerId, TriggerHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var result = await handler.DeleteAsync(workflowId, triggerId, UserId(http), ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "Trigger deleted" });
        }).RequireAuthorization().WithName("DeleteTrigger").WithTags("Triggers");
    }
}
