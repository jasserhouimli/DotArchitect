using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

/// <summary>Phase B: workflow.call starts child runs, passes payloads, and
/// publish-time cycle/target guards hold.</summary>
[Collection("api")]
public class WorkflowCallTests
{
    private readonly ReflowApiFactory _factory;

    public WorkflowCallTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> PublishAsync(HttpClient client, string name, object graph)
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, name);
        var put = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", graph);
        put.EnsureSuccessStatusCode();
        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    private static object CallGraph(Guid childId, string mode, object payload) => new
    {
        nodes = new[]
        {
            ApiHelpers.Node("seed", "data.csv.read", new { csvText = "a\n1" }),
            ApiHelpers.Node("call", "workflow.call", new
            {
                targetWorkflowId = childId.ToString(),
                mode,
                payload
            }),
            ApiHelpers.Node("out", "data.output", new { format = "json" })
        },
        edges = new object[]
        {
            new { sourceNodeId = "seed", targetNodeId = "call" },
            new { sourceNodeId = "call", targetNodeId = "out" }
        }
    };

    private static object PayloadChildGraph() => new
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

    private static async Task<JsonDocument> PollChildAsync(HttpClient client, Guid childId)
    {
        JsonDocument? found = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var runs = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{childId}/runs");
            var match = runs!.RootElement.EnumerateArray()
                .FirstOrDefault(r => r.GetProperty("triggerKind").GetString() == "workflow"
                    && r.GetProperty("status").GetInt32() >= 2);
            if (match.ValueKind != JsonValueKind.Undefined)
            {
                found = runs;
                break;
            }
            await Task.Delay(1000);
        }
        Assert.True(found is not null, "Child run did not finish in time");
        return found!;
    }

    [Fact]
    public async Task WaitMode_RunsChildWithMappedPayload()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var childId = await PublishAsync(client, "Child", PayloadChildGraph());
        var parentId = await PublishAsync(client, "Parent",
            CallGraph(childId, "wait", new { name = "Ada", age = 36, minAge = 18 }));

        var start = await client.PostAsync($"/api/v1/workflows/{parentId}/runs", null);
        start.EnsureSuccessStatusCode();
        var parentRun = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, parentRun, TimeSpan.FromSeconds(90));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        var childRuns = await PollChildAsync(client, childId);
        var child = childRuns.RootElement.EnumerateArray()
            .First(r => r.GetProperty("triggerKind").GetString() == "workflow");
        Assert.Equal(2, child.GetProperty("status").GetInt32());
        var childRunId = child.GetProperty("id").GetGuid();

        // The child consumed the mapped payload: Ada (36) passes minAge 18.
        var childTasks = (await (await client.GetAsync($"/api/v1/runs/{childRunId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        Assert.Equal(1, childTasks.Single(t => t.GetProperty("nodeId").GetString() == "out")
            .GetProperty("rowCount").GetInt32());

        // The parent call task outputs the child run id for downstream use.
        var parentTasks = (await (await client.GetAsync($"/api/v1/runs/{parentRun}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        Assert.Equal(1, parentTasks.Single(t => t.GetProperty("nodeId").GetString() == "call")
            .GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task ParentPayload_ExpressionsMapIntoChild()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var childId = await PublishAsync(client, "Child", PayloadChildGraph());

        // Parent receives a webhook payload and maps fields into the child.
        var parentId = await PublishAsync(client, "Parent", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("pick", "trigger.payload", new { rootPath = "" }),
                ApiHelpers.Node("call", "workflow.call", new
                {
                    targetWorkflowId = childId.ToString(),
                    mode = "wait",
                    payload = new
                    {
                        name = "{{ trigger.body.name }}",
                        age = "{{ trigger.body.age }}",
                        minAge = "{{ trigger.body.minAge }}"
                    }
                }),
                ApiHelpers.Node("out", "data.output", new { format = "json" })
            },
            edges = new object[]
            {
                new { sourceNodeId = "pick", targetNodeId = "call" },
                new { sourceNodeId = "call", targetNodeId = "out" }
            }
        });

        var hook = await client.PostAsJsonAsync($"/api/v1/workflows/{parentId}/triggers/webhooks", new
        {
            name = "H"
        });
        var path = new Uri((await hook.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("url").GetString()!).PathAndQuery;

        var fire = await _factory.CreateClient().PostAsJsonAsync(path, new
        {
            name = "Ada", age = 36, minAge = 40
        });
        Assert.Equal(HttpStatusCode.Accepted, fire.StatusCode);
        var parentRun = (await fire.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, parentRun, TimeSpan.FromSeconds(90));
        Assert.Equal(2, done.RootElement.GetProperty("status").GetInt32());

        // minAge 40 flowed parent -> child: Ada is filtered out downstream.
        var childRuns = await PollChildAsync(client, childId);
        var childRunId = childRuns.RootElement.EnumerateArray()
            .First(r => r.GetProperty("triggerKind").GetString() == "workflow")
            .GetProperty("id").GetGuid();
        var childTasks = (await (await client.GetAsync($"/api/v1/runs/{childRunId}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        Assert.Equal(0, childTasks.Single(t => t.GetProperty("nodeId").GetString() == "out")
            .GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task ChildFailure_FailsParentClearly()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var childId = await PublishAsync(client, "Doomed", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("bad", "data.validate", new
                {
                    requiredColumns = new[] { "missing_col" }
                })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "bad" } }
        });
        var parentId = await PublishAsync(client, "Parent",
            CallGraph(childId, "wait", new { a = 1 }));

        var start = await client.PostAsync($"/api/v1/workflows/{parentId}/runs", null);
        var parentRun = (await start.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetGuid();
        var done = await ApiHelpers.PollRunAsync(client, parentRun, TimeSpan.FromSeconds(90));
        Assert.Equal(3, done.RootElement.GetProperty("status").GetInt32());

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{parentRun}/tasks")).Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray().ToList();
        var call = tasks.Single(t => t.GetProperty("nodeId").GetString() == "call");
        Assert.Equal(4, call.GetProperty("status").GetInt32());
        Assert.Contains("Child run", call.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PublishGuards_CycleSelfAndUnpublished()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);

        var a = await PublishAsync(client, "A", new
        {
            nodes = new[] { ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }) },
            edges = Array.Empty<object>()
        });

        // B calls A: fine.
        var b = await ApiHelpers.CreateWorkflowAsync(client, "B");
        await client.PutAsJsonAsync($"/api/v1/workflows/{b}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("call", "workflow.call", new { targetWorkflowId = a.ToString(), mode = "wait" })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "call" } }
        });
        (await client.PostAsync($"/api/v1/workflows/{b}/publish", null)).EnsureSuccessStatusCode();

        // A now calls B: cycle.
        await client.PutAsJsonAsync($"/api/v1/workflows/{a}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("call", "workflow.call", new { targetWorkflowId = b.ToString(), mode = "wait" })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "call" } }
        });
        var cycle = await client.PostAsync($"/api/v1/workflows/{a}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Contains("cycle", await cycle.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        // Self call.
        var c = await ApiHelpers.CreateWorkflowAsync(client, "C");
        await client.PutAsJsonAsync($"/api/v1/workflows/{c}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("call", "workflow.call", new { targetWorkflowId = c.ToString(), mode = "wait" })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "call" } }
        });
        var self = await client.PostAsync($"/api/v1/workflows/{c}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        // Unpublished target.
        var e = await ApiHelpers.CreateWorkflowAsync(client, "E");
        var d = await ApiHelpers.CreateWorkflowAsync(client, "D");
        await client.PutAsJsonAsync($"/api/v1/workflows/{d}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("read", "data.csv.read", new { csvText = "a\n1" }),
                ApiHelpers.Node("call", "workflow.call", new { targetWorkflowId = e.ToString(), mode = "wait" })
            },
            edges = new object[] { new { sourceNodeId = "read", targetNodeId = "call" } }
        });
        var unpub = await client.PostAsync($"/api/v1/workflows/{d}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, unpub.StatusCode);
        Assert.Contains("publish", await unpub.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
