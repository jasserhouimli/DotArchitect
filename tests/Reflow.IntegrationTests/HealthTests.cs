using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class HealthTests
{
    private readonly ReflowApiFactory _factory;

    public HealthTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReportsHealthyWithReachableDatabase()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("healthy", body!.RootElement.GetProperty("status").GetString());
        Assert.Equal("reachable", body.RootElement.GetProperty("database").GetString());
    }
}
