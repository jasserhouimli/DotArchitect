namespace Reflow.Modules.Notifications.Services;

public static class WebhookPayload
{
    public static object Build(
        string url,
        string eventName,
        string title,
        string message,
        Guid workflowId,
        string workflowName,
        Guid runId,
        int version,
        bool succeeded,
        int total,
        int completed,
        int failed,
        int rejected,
        string? error)
    {
        var host = string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            host = uri.Host;

        var headline = $"{title} — {message}";
        if (host.Contains("discord.com", StringComparison.OrdinalIgnoreCase))
            return new { content = headline };
        if (host.Contains("hooks.slack.com", StringComparison.OrdinalIgnoreCase))
            return new { text = headline };

        return new
        {
            @event = eventName,
            workflowId,
            workflowName,
            runId,
            version,
            succeeded,
            summary = new { total, completed, failed, rejected },
            error,
            timestamp = DateTime.UtcNow
        };
    }
}
