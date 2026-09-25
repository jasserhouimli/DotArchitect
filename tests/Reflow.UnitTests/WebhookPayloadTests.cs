using System.Text.Json;
using Reflow.Modules.Notifications.Services;
using Xunit;

namespace Reflow.UnitTests;

public class WebhookPayloadTests
{
    private static string Prop(object payload, string name)
    {
        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(name).GetString()!;
    }

    [Fact]
    public void Discord_GetsContentField()
    {
        var payload = WebhookPayload.Build("https://discord.com/api/webhooks/123/abc", "run.failed",
            "Run failed: Orders v1", "Run abc failed: boom",
            Guid.NewGuid(), "Orders", Guid.NewGuid(), 1, false, 3, 2, 1, 0, "boom");

        Assert.Equal("Run failed: Orders v1 — Run abc failed: boom", Prop(payload, "content"));
    }

    [Fact]
    public void Slack_GetsTextField()
    {
        var payload = WebhookPayload.Build("https://hooks.slack.com/services/T/B/X", "run.succeeded",
            "Run succeeded: Orders v2", "Run abc completed",
            Guid.NewGuid(), "Orders", Guid.NewGuid(), 2, true, 3, 3, 0, 1, null);

        Assert.Equal("Run succeeded: Orders v2 — Run abc completed", Prop(payload, "text"));
    }

    [Fact]
    public void Generic_GetsFullStructuredPayload()
    {
        var runId = Guid.NewGuid();
        var payload = WebhookPayload.Build("https://example.com/hook", "run.succeeded",
            "Run succeeded", "Done",
            Guid.NewGuid(), "Orders", runId, 1, true, 2, 2, 0, 5, null);

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("run.succeeded", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal(runId, doc.RootElement.GetProperty("runId").GetGuid());
        Assert.Equal(5, doc.RootElement.GetProperty("summary").GetProperty("rejected").GetInt32());
    }
}
