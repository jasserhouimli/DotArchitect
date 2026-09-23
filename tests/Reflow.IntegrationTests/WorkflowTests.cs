using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class WorkflowTests
{
    private readonly ReflowApiFactory _factory;

    public WorkflowTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateListGet_Roundtrip()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);

        var id = await ApiHelpers.CreateWorkflowAsync(client, "Roundtrip");

        var list = await client.GetFromJsonAsync<JsonDocument>("/api/v1/workflows");
        Assert.Contains(list!.RootElement.EnumerateArray(),
            w => w.GetProperty("id").GetGuid() == id);

        var get = await client.GetAsync($"/api/v1/workflows/{id}");
        get.EnsureSuccessStatusCode();
        var detail = await get.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("Roundtrip", detail!.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_UnsupportedNodeType_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);

        var res = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[] { ApiHelpers.Node("n1", "python.exec", new { }) },
            edges = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Validate_EmptyWorkflow_IsInvalid()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);

        var res = await client.PostAsync($"/api/v1/workflows/{id}/validate", null);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.False(body!.RootElement.GetProperty("isValid").GetBoolean());
        Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Validate_Cycle_IsInvalid()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("a", "data.output", new { }),
                ApiHelpers.Node("b", "data.output", new { })
            },
            edges = new[]
            {
                new { sourceNodeId = "a", targetNodeId = "b" },
                new { sourceNodeId = "b", targetNodeId = "a" }
            }
        });

        var res = await client.PostAsync($"/api/v1/workflows/{id}/validate", null);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.False(body!.RootElement.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task Publish_CreatesVersions()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[] { ApiHelpers.Node("out", "data.output", new { format = "json" }) },
            edges = Array.Empty<object>()
        });

        var pub1 = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        pub1.EnsureSuccessStatusCode();
        var v1 = await pub1.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, v1!.RootElement.GetProperty("version").GetInt32());

        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[]
            {
                ApiHelpers.Node("out", "data.output", new { format = "json" }),
                ApiHelpers.Node("out2", "data.output", new { format = "csv" })
            },
            edges = Array.Empty<object>()
        });
        var pub2 = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        var v2 = await pub2.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(2, v2!.RootElement.GetProperty("version").GetInt32());

        var versions = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{id}/versions");
        Assert.Equal(2, versions!.RootElement.GetArrayLength());

        var version = await client.GetFromJsonAsync<JsonDocument>($"/api/v1/workflows/{id}/versions/1");
        Assert.Contains("out", version!.RootElement.GetProperty("definitionJson").GetString());
    }

    [Fact]
    public async Task Publish_InvalidWorkflow_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);

        var res = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Run_UnpublishedWorkflow_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);

        var res = await client.PostAsync($"/api/v1/workflows/{id}/runs", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Archive_BlocksEditPublishAndRun()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);
        await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new
        {
            nodes = new[] { ApiHelpers.Node("out", "data.output", new { }) },
            edges = Array.Empty<object>()
        });
        await client.PostAsync($"/api/v1/workflows/{id}/publish", null);

        var archive = await client.PostAsync($"/api/v1/workflows/{id}/archive", null);
        archive.EnsureSuccessStatusCode();

        var edit = await client.PutAsJsonAsync($"/api/v1/workflows/{id}", new { name = "Changed" });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);

        var publish = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);

        var run = await client.PostAsync($"/api/v1/workflows/{id}/runs", null);
        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesWorkflow()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await ApiHelpers.CreateWorkflowAsync(client);

        var del = await client.DeleteAsync($"/api/v1/workflows/{id}");
        del.EnsureSuccessStatusCode();

        var get = await client.GetAsync($"/api/v1/workflows/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
