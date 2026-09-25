using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Reflow.Modules.Triggers.Features.Webhooks;

public static class ReceiveWebhookEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/hooks/{token}", async (
            string token,
            ReceiveWebhookHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            string? payload = null;
            if (http.Request.ContentLength.GetValueOrDefault() > 0)
            {
                using var reader = new StreamReader(http.Request.Body);
                payload = await reader.ReadToEndAsync(ct);
                if (string.IsNullOrWhiteSpace(payload))
                    payload = null;
            }

            var result = await handler.Handle(token, payload, ct);
            return result.StatusCode switch
            {
                201 => Results.Json(new { id = result.Value }, statusCode: 202),
                404 => Results.NotFound(new { error = result.Error }),
                429 => Results.Json(new { error = result.Error }, statusCode: 429),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting("webhook")
        .WithMetadata(new RequestSizeLimitAttribute(600 * 1024))
        .WithName("ReceiveWebhook")
        .WithTags("Triggers");
    }
}
