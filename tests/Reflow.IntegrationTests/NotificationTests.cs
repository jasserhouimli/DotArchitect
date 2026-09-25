using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.Notifications.Persistence;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class NotificationTests
{
    private readonly ReflowApiFactory _factory;

    public NotificationTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<JsonDocument> WaitForInboxAsync(HttpClient client, Guid runId, int kind, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var inbox = await client.GetFromJsonAsync<JsonDocument>("/api/v1/notifications?unreadOnly=true");
            var match = inbox!.RootElement.EnumerateArray().FirstOrDefault(n =>
                n.GetProperty("workflowRunId").GetGuid() == runId &&
                n.GetProperty("kind").GetInt32() == kind);
            if (match.ValueKind != JsonValueKind.Undefined)
                return inbox;
            if (DateTime.UtcNow > deadline)
                return inbox;
            await Task.Delay(500);
        }
    }

    private static bool HasInbox(JsonDocument inbox, Guid runId, int kind) =>
        inbox.RootElement.EnumerateArray().Any(n =>
            n.GetProperty("workflowRunId").GetGuid() == runId &&
            n.GetProperty("kind").GetInt32() == kind);

    private static async Task<Guid> PublishSimpleWorkflowAsync(HttpClient client)
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, "Notified");
        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[] { ApiHelpers.Node("read", "data.csv.read", new { csvText = "a,b\n1,2" }) },
            edges = Array.Empty<object>()
        });
        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task RuleCrud_Works()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);

        var missing = await client.GetAsync($"/api/v1/workflows/{workflowId}/notifications/rule");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var save = await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = true,
            notifyOnFailure = true,
            rejectsAbove = 10,
            webhookUrl = (string?)null
        });
        save.EnsureSuccessStatusCode();
        var rule = await save.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(rule!.RootElement.GetProperty("notifyOnSuccess").GetBoolean());
        Assert.Equal(10, rule.RootElement.GetProperty("rejectsAbove").GetInt32());

        var get = await client.GetAsync($"/api/v1/workflows/{workflowId}/notifications/rule");
        get.EnsureSuccessStatusCode();

        var delete = await client.DeleteAsync($"/api/v1/workflows/{workflowId}/notifications/rule");
        delete.EnsureSuccessStatusCode();

        var gone = await client.GetAsync($"/api/v1/workflows/{workflowId}/notifications/rule");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Rule_Validation_RejectsBadInput()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);

        // No trigger enabled
        var empty = await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = false,
            rejectsAbove = (int?)null,
            webhookUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        // Bad webhook URL
        var badUrl = await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = true,
            rejectsAbove = (int?)null,
            webhookUrl = "ftp://example.com/hook"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badUrl.StatusCode);

        // Negative threshold
        var badThreshold = await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = false,
            rejectsAbove = -1,
            webhookUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, badThreshold.StatusCode);

        // Foreign workflow
        var foreign = await client.PutAsJsonAsync($"/api/v1/workflows/{Guid.NewGuid()}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = true,
            rejectsAbove = (int?)null,
            webhookUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task SuccessfulRun_CreatesInboxNotification()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishSimpleWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = true,
            notifyOnFailure = false,
            rejectsAbove = (int?)null,
            webhookUrl = (string?)null
        });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        var inbox = await WaitForInboxAsync(client, runId, 0, TimeSpan.FromSeconds(15));
        var items = inbox.RootElement.EnumerateArray().ToList();
        var match = items.FirstOrDefault(n =>
            n.GetProperty("workflowRunId").GetGuid() == runId &&
            n.GetProperty("kind").GetInt32() == 0);
        Assert.True(match.ValueKind != JsonValueKind.Undefined, "Expected a success notification in the inbox");

        var count = await client.GetFromJsonAsync<JsonDocument>("/api/v1/notifications/unread-count");
        Assert.True(count!.RootElement.GetProperty("unread").GetInt32() >= 1);

        await client.PostAsync($"/api/v1/notifications/{match.GetProperty("id").GetGuid()}/read", null);
        var after = await client.GetFromJsonAsync<JsonDocument>("/api/v1/notifications?unreadOnly=true");
        Assert.DoesNotContain(after!.RootElement.EnumerateArray(),
            n => n.GetProperty("id").GetGuid() == match.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task FailedRun_CreatesFailureNotification()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client, "Failing");
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("val", "data.validate", new { requiredColumns = new[] { "ghost" } })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "val" } }
        });
        await client.PostAsync($"/api/v1/workflows/{workflowId}/publish", null);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = true,
            rejectsAbove = (int?)null,
            webhookUrl = (string?)null
        });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(3, done.RootElement.GetProperty("status").GetInt32());

        var inbox = await WaitForInboxAsync(client, runId, 1, TimeSpan.FromSeconds(15));
        Assert.True(HasInbox(inbox, runId, 1), "Expected a failure notification in the inbox");
    }

    [Fact]
    public async Task ThresholdBreach_CreatesThresholdNotification()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client, "Rejects");
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a,b\n1,x\n,y" }),
                ApiHelpers.Node("val", "data.validate", new { requiredColumns = new[] { "a" } })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "val" } }
        });
        await client.PostAsync($"/api/v1/workflows/{workflowId}/publish", null);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = false,
            rejectsAbove = 0,
            webhookUrl = (string?)null
        });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        var inbox = await WaitForInboxAsync(client, runId, 2, TimeSpan.FromSeconds(15));
        Assert.True(HasInbox(inbox, runId, 2), "Expected a threshold notification in the inbox");
    }

    [Fact]
    public async Task NoRule_NoNotifications()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishSimpleWorkflowAsync(client);

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));

        var inbox = await client.GetFromJsonAsync<JsonDocument>("/api/v1/notifications?unreadOnly=true");
        Assert.Empty(inbox!.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task DeleteWorkflow_RemovesItsRules()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}/notifications/rule", new
        {
            notifyOnSuccess = false,
            notifyOnFailure = true,
            rejectsAbove = (int?)null,
            webhookUrl = (string?)null
        });

        var del = await client.DeleteAsync($"/api/v1/workflows/{workflowId}");
        del.EnsureSuccessStatusCode();

        var get = await client.GetAsync($"/api/v1/workflows/{workflowId}/notifications/rule");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Assert.Empty(db.NotificationRules.Where(r => r.WorkflowId == workflowId));
    }
}
