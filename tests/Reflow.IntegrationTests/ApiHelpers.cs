using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Reflow.IntegrationTests;

public static class ApiHelpers
{
    public static async Task<(HttpClient Client, string Token)> LoginNewUserWithTokenAsync(ReflowApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Test1234",
            displayName = "Tester"
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "Test1234"
        });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonDocument>();
        var token = body!.RootElement.GetProperty("accessToken").GetString()!;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (authed, token);
    }

    public static async Task<HttpClient> LoginNewUserAsync(ReflowApiFactory factory)
    {
        var (client, _) = await LoginNewUserWithTokenAsync(factory);
        return client;
    }

    public static async Task<Guid> CreateWorkflowAsync(HttpClient client, string name = "Test workflow")
    {
        var res = await client.PostAsJsonAsync("/api/v1/workflows", new { name });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetGuid();
    }

    public static object Node(string nodeId, string nodeType, object config, double x = 50, double y = 50) =>
        new
        {
            nodeId,
            nodeType,
            configJson = JsonSerializer.Serialize(config),
            label = nodeId,
            positionX = x,
            positionY = y
        };

    public static async Task<JsonDocument> PollRunAsync(HttpClient client, Guid runId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var res = await client.GetAsync($"/api/v1/runs/{runId}");
            res.EnsureSuccessStatusCode();
            var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
            var status = body.RootElement.GetProperty("status").GetInt32();
            if (status is 2 or 3 or 4 || DateTime.UtcNow > deadline)
                return body;
            await Task.Delay(1000);
        }
    }
}
