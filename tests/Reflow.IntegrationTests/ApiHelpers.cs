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
}
