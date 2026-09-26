using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

/// <summary>Phase C1: isolated per-node test runs with sample payloads/inputs.</summary>
[Collection("api")]
public class NodeTestRunTests
{
    private readonly ReflowApiFactory _factory;

    public NodeTestRunTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> CreateGraphAsync(HttpClient client, object graph)
    {
        var id = await ApiHelpers.CreateWorkflowAsync(client, "Testable");
        var put = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", graph);
        put.EnsureSuccessStatusCode();
        return id;
    }

    private static object Graph() => new
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
            ApiHelpers.Node("call", "workflow.call", new
            {
                targetWorkflowId = Guid.NewGuid().ToString(),
                mode = "wait"
            })
        },
        edges = new object[]
        {
            new { sourceNodeId = "pick", targetNodeId = "adults" }
        }
    };

    private static async Task<JsonDocument> TestAsync(HttpClient client, Guid workflowId, string nodeId, object body)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/nodes/{nodeId}/test", body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
    }

    [Fact]
    public async Task SourceNode_RunsWithSamplePayload()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await CreateGraphAsync(client, Graph());

        var body = await TestAsync(client, workflowId, "pick", new
        {
            samplePayload = new { name = "Ada", age = 36, minAge = 18 }
        });
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(3, body.RootElement.GetProperty("columns").GetArrayLength());
        Assert.Equal(1, body.RootElement.GetProperty("totalRows").GetInt32());
    }

    [Fact]
    public async Task TransformNode_RunsWithSuppliedInputsAndExpressions()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await CreateGraphAsync(client, Graph());

        var body = await TestAsync(client, workflowId, "adults", new
        {
            samplePayload = new { minAge = 18 },
            inputs = new[]
            {
                new
                {
                    columns = new[] { "name", "age" },
                    rows = new object[] { new object[] { "Ada", "36" }, new object[] { "Bob", "15" } }
                }
            }
        });
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, body.RootElement.GetProperty("totalRows").GetInt32());
    }

    [Fact]
    public async Task MissingInputs_FailsClearly()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await CreateGraphAsync(client, Graph());

        var body = await TestAsync(client, workflowId, "adults", new
        {
            samplePayload = new { minAge = 18 }
        });
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.NotNull(body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SideEffectingNode_Rejected()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await CreateGraphAsync(client, Graph());

        var res = await client.PostAsJsonAsync(
            $"/api/v1/workflows/{workflowId}/nodes/call/test", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UnknownNode_Returns404()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var workflowId = await CreateGraphAsync(client, Graph());

        var res = await client.PostAsJsonAsync(
            $"/api/v1/workflows/{workflowId}/nodes/nope/test", new { });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
