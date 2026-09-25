using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reflow.Infrastructure.Http;

namespace Reflow.Modules.Notifications.Services;

public class WebhookSender(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<WebhookSender> logger)
{
    public async Task<bool> SendAsync(string url, object payload, CancellationToken ct)
    {
        var allowPrivate = configuration.GetValue<bool>("WorkflowExecution:AllowPrivateNetwork");
        try
        {
            await SsrfGuard.AssertSafeAsync(url, allowPrivate, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Notification webhook blocked: {Reason}", ex.Message);
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var client = httpFactory.CreateClient("reflow-http");
            using var content = JsonContent.Create(payload);
            using var response = await client.PostAsync(url, content, timeoutCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            logger.LogWarning("Notification webhook delivery failed");
            return false;
        }
    }
}
