using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

/// <summary>
/// Phase A: {{ trigger.* }} / {{ run.* }} expressions resolve inside any
/// node config at execution time; bad expressions fail validation.
/// </summary>
[Collection("api")]
public class ExpressionRunTests
{
    private readonly ReflowApiFactory _factory;

    public ExpressionRunTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> PublishGraphAsync(HttpClient client, string name, object graph)
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, name);
        var put = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", graph);
        put.EnsureSuccessStatusCode();
        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    private static object PayloadFilterGraph() => new
    {
        nodes = new[]
        {
            ApiHelpers.Node("pick", "trigger.payload", new { rootPath = "" }),
            ApiHelpers.Node("adults", "data.filter", new
            {
                column = "age",
                @operator = "greaterThan",
                value = "{{ trigger.body.minAge }}"
            }),
            ApiHelpers.Node("out", "data.output", new { format = "json" })
        },
        edges = new object[]
        {
            new { sourceNodeId = "pick", targetNodeId = "adults" },
            new { sourceNodeId = "adults", targetNodeId = "out" }
        }
    };

    [Fact]
    public async Task WebhookPayload_DrivesFilterThreshold()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishGraphAsync(client, "Expr", PayloadFilterGraph());

        var hook = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/webhooks", new
        {
            name = "Expr"
        });
        var path = new Uri((await hook.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("url").GetString()!).PathAndQuery;
        var anon = _factory.CreateClient();

        // minAge 18 keeps Ada (36).
        var fire1 = await anon.PostAsJsonAsync(path, new
        {
            name = "Ada", age = 36, minAge = 18
        });
        Assert.Equal(HttpStatusCode.Accepted, fire1.StatusCode);
        var run1 = (await fire1.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done1 = await ApiHelpers.PollRunAsync(client, run1, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done1.RootElement.GetProperty("status").GetInt32());

        var tasks1 = (await (await client.GetAsync($"/api/v1/runs/{run1}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        Assert.Equal(1, tasks1.Single(t => t.GetProperty("nodeId").GetString() == "out")
            .GetProperty("rowCount").GetInt32());

        // minAge 40 drops her: same workflow, dynamic threshold, no republish.
        var fire2 = await anon.PostAsJsonAsync(path, new
        {
            name = "Ada", age = 36, minAge = 40
        });
        var run2 = (await fire2.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done2 = await ApiHelpers.PollRunAsync(client, run2, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done2.RootElement.GetProperty("status").GetInt32());

        var tasks2 = (await (await client.GetAsync($"/api/v1/runs/{run2}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        Assert.Equal(0, tasks2.Single(t => t.GetProperty("nodeId").GetString() == "out")
            .GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task BadExpression_FailsValidation()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("f", "data.filter", new
                {
                    column = "a",
                    @operator = "equals",
                    value = "{{ bogus.root }}"
                })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "f" } }
        });

        var validate = await client.PostAsync($"/api/v1/workflows/{workflowId}/validate", null);
        validate.EnsureSuccessStatusCode();
        var body = await validate.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(body!.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Contains("bogus", body.RootElement.GetProperty("errors").GetRawText());
    }

    [Fact]
    public async Task MissingPayloadProperty_FailsRunClearly()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishGraphAsync(client, "ExprMissing", PayloadFilterGraph());

        var hook = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/triggers/webhooks", new
        {
            name = "Expr"
        });
        var path = new Uri((await hook.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("url").GetString()!).PathAndQuery;

        // No minAge in the body: the filter task must fail with a clear error.
        var fire = await _factory.CreateClient().PostAsJsonAsync(path, new
        {
            name = "Ada", age = 36
        });
        var runId = (await fire.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(3, done.RootElement.GetProperty("status").GetInt32());

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        var failed = tasks.Single(t => t.GetProperty("nodeId").GetString() == "adults");
        Assert.Equal(4, failed.GetProperty("status").GetInt32());
        Assert.Contains("minAge", failed.GetProperty("error").GetString());
    }
}
