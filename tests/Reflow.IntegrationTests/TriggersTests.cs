using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class TriggersTests
{
    private readonly ReflowApiFactory _factory;

    public TriggersTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> PublishSimpleWorkflowAsync(HttpClient client, string name = "Triggered")
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, name);
        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "out" } }
        });
        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task ScheduleCrud_Works()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);

        var empty = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{workflowId}/triggers");
        Assert.Empty(empty!.RootElement.EnumerateArray());

        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Nightly",
            cronExpression = "0 8 * * *",
            timezone = "UTC",
            overlapPolicy = 0,
            isEnabled = true
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var triggerId = created!.RootElement.GetProperty("id").GetGuid();
        Assert.NotNull(created.RootElement.GetProperty("nextRunAt").GetString());

        var pause = await client.PutAsJsonAsync(
            $"/api/v1/workflows/{workflowId}/triggers/schedules/{triggerId}", new { isEnabled = false });
        pause.EnsureSuccessStatusCode();
        var paused = await pause.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(paused!.RootElement.GetProperty("isEnabled").GetBoolean());

        var list = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{workflowId}/triggers");
        Assert.Single(list!.RootElement.EnumerateArray());

        var delete = await client.DeleteAsync($"/api/v1/workflows/{workflowId}/triggers/{triggerId}");
        delete.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Schedule_Validation_RejectsBadInput()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);

        var badCron = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Bad",
            cronExpression = "not a cron",
            timezone = "UTC",
            overlapPolicy = 0,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, badCron.StatusCode);

        var badTz = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Bad",
            cronExpression = "0 8 * * *",
            timezone = "Mars/Olympus",
            overlapPolicy = 0,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, badTz.StatusCode);

        var badOverlap = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Bad",
            cronExpression = "0 8 * * *",
            timezone = "UTC",
            overlapPolicy = 7,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, badOverlap.StatusCode);

        var foreign = await client.PostAsJsonAsync($"/api/v1/workflows/{Guid.NewGuid()}/triggers/schedules", new
        {
            name = "Bad",
            cronExpression = "0 8 * * *",
            timezone = "UTC",
            overlapPolicy = 0,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task WebhookLifecycle_Works()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);

        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/webhooks", new
        {
            name = "Deploy hook"
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var triggerId = created!.RootElement.GetProperty("id").GetGuid();
        var url1 = created.RootElement.GetProperty("url").GetString()!;
        Assert.Contains("/api/v1/hooks/", url1);

        var list = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{workflowId}/triggers");
        var listed = list!.RootElement.EnumerateArray().ToList();
        Assert.Single(listed);
        Assert.DoesNotContain("hooks/", listed[0].GetRawText());

        var regen = await client.PostAsync(
            $"/api/v1/workflows/{workflowId}/triggers/webhooks/{triggerId}/regenerate", null);
        regen.EnsureSuccessStatusCode();
        var regenerated = await regen.Content.ReadFromJsonAsync<JsonDocument>();
        var url2 = regenerated!.RootElement.GetProperty("url").GetString()!;
        Assert.NotEqual(url1, url2);

        // Old token is dead.
        var oldToken = url1.Split("/hooks/")[1];
        var dead = await _factory.CreateClient().PostAsync($"/api/v1/hooks/{oldToken}",
            JsonContent.Create(new { a = 1 }));
        Assert.Equal(HttpStatusCode.NotFound, dead.StatusCode);
    }

    [Fact]
    public async Task InboundWebhook_StartsRunWithPayload()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client, "Hooked");
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("pick", "trigger.payload", new { rootPath = "" }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            edges = new object[] { new { sourceNodeId = "pick", targetNodeId = "out" } }
        });
        await client.PostAsync($"/api/v1/workflows/{workflowId}/publish", null);

        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/webhooks", new
        {
            name = "Hook"
        });
        var url = (await create.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("url").GetString()!;
        var path = new Uri(url).PathAndQuery;

        var anon = _factory.CreateClient();
        var fire = await anon.PostAsJsonAsync(path, new { name = "Ada", age = 36 });
        Assert.True(fire.StatusCode == HttpStatusCode.Accepted, $"Unexpected status: {fire.StatusCode}");
        var runId = (await fire.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        var run = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/runs/{runId}");
        Assert.Equal("webhook", run!.RootElement.GetProperty("triggerKind").GetString());
        Assert.Equal("Hook", run.RootElement.GetProperty("triggerName").GetString());

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        var pick = tasks.Single(t => t.GetProperty("nodeId").GetString() == "pick");
        Assert.Equal(1, pick.GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task InboundWebhook_RejectsBadInput()
    {
        var anon = _factory.CreateClient();

        var unknown = await anon.PostAsJsonAsync("/api/v1/hooks/does-not-exist", new { a = 1 });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishSimpleWorkflowAsync(client);
        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/webhooks", new
        {
            name = "Hook"
        });
        var url = (await create.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("url").GetString()!;
        var path = new Uri(url).PathAndQuery;

        // Non-JSON body is rejected.
        var bad = await anon.PostAsync(path, new StringContent("this is not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Scheduler_FiresDueSchedule()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishSimpleWorkflowAsync(client);

        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Every minute",
            cronExpression = "* * * * *",
            timezone = "UTC",
            overlapPolicy = 0,
            isEnabled = true
        });
        create.EnsureSuccessStatusCode();
        var triggerId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        try
        {
            JsonDocument? found = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                var runs = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{workflowId}/runs");
                var scheduled = runs!.RootElement.EnumerateArray()
                    .FirstOrDefault(r => r.GetProperty("triggerKind").GetString() == "schedule");
                if (scheduled.ValueKind != JsonValueKind.Undefined)
                {
                    found = runs;
                    break;
                }
                await Task.Delay(3000);
            }

            Assert.True(found is not null, "Scheduler did not fire within 90s");
            var match = found!.RootElement.EnumerateArray()
                .First(r => r.GetProperty("triggerKind").GetString() == "schedule");
            Assert.Equal("Every minute", match.GetProperty("triggerName").GetString());
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/workflows/{workflowId}/triggers/{triggerId}");
        }
    }

    [Fact]
    public async Task DisabledSchedule_NeverFires()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishSimpleWorkflowAsync(client);

        var create = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/schedules", new
        {
            name = "Paused",
            cronExpression = "* * * * *",
            timezone = "UTC",
            overlapPolicy = 0,
            isEnabled = false
        });
        create.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(6));

        var runs = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{workflowId}/runs");
        Assert.Empty(runs!.RootElement.EnumerateArray());
    }
}
