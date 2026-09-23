using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class RunTests
{
    private readonly ReflowApiFactory _factory;

    public RunTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> PublishPipelineAsync(HttpClient client, object[] nodes, object[] edges)
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, "Pipeline");
        var update = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new { nodes, edges });
        update.EnsureSuccessStatusCode();
        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task FullPipeline_CompletesWithQualityCounts()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishPipelineAsync(client,
            new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "region,amount\nnorth,10\nsouth,25\n,7" }),
                ApiHelpers.Node("val", "data.validate", new { requiredColumns = new[] { "region", "amount" } }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            new object[]
            {
                new { sourceNodeId = "read", targetNodeId = "val" },
                new { sourceNodeId = "val", targetNodeId = "out" }
            });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        start.EnsureSuccessStatusCode();
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        var run = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));

        Assert.Equal(2, run.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(3, run.RootElement.GetProperty("totalTasks").GetInt32());
        Assert.Equal(3, run.RootElement.GetProperty("completedTasks").GetInt32());

        var tasksRes = await client.GetAsync($"/api/v1/runs/{runId}/tasks");
        tasksRes.EnsureSuccessStatusCode();
        var tasks = (await tasksRes.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.EnumerateArray().ToList();

        Assert.All(tasks, t => Assert.Equal(3, t.GetProperty("status").GetInt32()));

        var validate = tasks.Single(t => t.GetProperty("nodeId").GetString() == "val");
        Assert.Contains("rejected=1", validate.GetProperty("outputSummary").GetString());
        Assert.Equal(2, validate.GetProperty("rowCount").GetInt32());

        var output = tasks.Single(t => t.GetProperty("nodeId").GetString() == "out");
        var artifact = await client.GetAsync($"/api/v1/runs/{runId}/artifacts/out");
        artifact.EnsureSuccessStatusCode();
        var content = await artifact.Content.ReadAsStringAsync();
        Assert.Contains("north", content);
        Assert.Contains("south", content);
        Assert.DoesNotContain("\"\",\"7\"", content);
    }

    [Fact]
    public async Task FailedRun_RecordsError_AndRetryCreatesNewAttempt()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishPipelineAsync(client,
            new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a,b\n1,2" }),
                ApiHelpers.Node("val", "data.validate", new { requiredColumns = new[] { "ghost" } })
            },
            new object[] { new { sourceNodeId = "read", targetNodeId = "val" } });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        var failed = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));

        Assert.Equal(3, failed.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("ghost", failed.RootElement.GetProperty("error").GetString());

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        var failedTask = tasks.Single(t => t.GetProperty("nodeId").GetString() == "val");
        Assert.Equal(4, failedTask.GetProperty("status").GetInt32());
        var taskId = failedTask.GetProperty("id").GetGuid();

        var attemptsBefore = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/tasks/{taskId}/attempts");
        Assert.Single(attemptsBefore!.RootElement.EnumerateArray());

        var retry = await client.PostAsync($"/api/v1/tasks/{taskId}/retry", null);
        retry.EnsureSuccessStatusCode();

        var failedAgain = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(3, failedAgain.RootElement.GetProperty("status").GetInt32());

        var attemptsAfter = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/tasks/{taskId}/attempts");
        Assert.Equal(2, attemptsAfter!.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Retry_NonFailedTask_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishPipelineAsync(client,
            new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            new object[] { new { sourceNodeId = "read", targetNodeId = "out" } });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        var taskId = tasks[0].GetProperty("id").GetGuid();

        var retry = await client.PostAsync($"/api/v1/tasks/{taskId}/retry", null);

        Assert.Equal(HttpStatusCode.BadRequest, retry.StatusCode);
    }

    [Fact]
    public async Task Cancel_CompletedRun_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await PublishPipelineAsync(client,
            new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            new object[] { new { sourceNodeId = "read", targetNodeId = "out" } });

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, runId, TimeSpan.FromSeconds(60));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        var cancel = await client.PostAsync($"/api/v1/runs/{runId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }
}
