using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class RealtimeTests
{
    private readonly ReflowApiFactory _factory;

    public RealtimeTests(ReflowApiFactory factory) => _factory = factory;

    private HubConnection BuildConnection(string? token)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/runs"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    [Fact]
    public async Task JoinRun_ForeignRun_IsRejected()
    {
        var (alice, _) = await ApiHelpers.LoginNewUserWithTokenAsync(_factory);
        var (_, bobToken) = await ApiHelpers.LoginNewUserWithTokenAsync(_factory);

        var workflowId = await ApiHelpers.CreateWorkflowAsync(alice);
        await alice.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[] { ApiHelpers.Node("out", "data.output", new { format = "json" }) },
            edges = Array.Empty<object>()
        });
        await alice.PostAsync($"/api/v1/workflows/{workflowId}/publish", null);
        var start = await alice.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        // Bob is authenticated but does not own the run: same "not found", no oracle.
        await using var conn = BuildConnection(bobToken);
        await conn.StartAsync();
        var ex = await Assert.ThrowsAsync<HubException>(() => conn.InvokeAsync("JoinRun", runId));
        Assert.Contains("Run not found", ex.Message);
    }

    [Fact]
    public async Task RunLifecycle_StreamsTaskLogAndRunEvents()
    {
        var (client, token) = await ApiHelpers.LoginNewUserWithTokenAsync(_factory);
        var workflowId = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{workflowId}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a,b\n1,2\n3,4" }),
                ApiHelpers.Node("pass", "data.limit", new { count = 10, offset = 0 }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            edges = new object[] {
                new { sourceNodeId = "read", targetNodeId = "pass" },
                new { sourceNodeId = "pass", targetNodeId = "out" }
            }
        });
        await client.PostAsync($"/api/v1/workflows/{workflowId}/publish", null);

        // Connect BEFORE starting the run: SignalR only delivers post-join
        // events, and a fast worker can finish small runs in milliseconds.
        // The three-node chain keeps the run alive across several worker
        // batches so every lifecycle event lands after the join.
        var tasksSeen = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var logsSeen = new System.Collections.Concurrent.ConcurrentBag<string>();
        var runDone = new TaskCompletionSource<int>();

        await using var conn = BuildConnection(token);
        conn.On<JsonDocument>("taskUpdated", doc =>
        {
            tasksSeen.Add(doc.RootElement.GetProperty("id").GetGuid());
        });
        conn.On<JsonDocument>("logAppended", doc =>
        {
            logsSeen.Add(doc.RootElement.GetProperty("message").GetString()!);
        });
        conn.On<JsonDocument>("runUpdated", doc =>
        {
            var status = doc.RootElement.GetProperty("status").GetInt32();
            if (status is 2 or 3 or 4)
                runDone.TrySetResult(status);
        });

        await conn.StartAsync();

        var start = await client.PostAsync($"/api/v1/workflows/{workflowId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();

        await conn.InvokeAsync("JoinRun", runId);

        var completed = await Task.WhenAny(runDone.Task, Task.Delay(TimeSpan.FromSeconds(45)));
        Assert.Same(runDone.Task, completed);
        Assert.Equal(2, await runDone.Task);

        Assert.NotEmpty(tasksSeen);
        Assert.NotEmpty(logsSeen);

        var taskList = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(3, taskList.Count);
        Assert.Subset(tasksSeen.ToHashSet(), taskList.ToHashSet());
    }
}
