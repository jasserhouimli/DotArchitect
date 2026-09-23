using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class AuthTests
{
    private readonly ReflowApiFactory _factory;

    public AuthTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Returns201()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"new{Guid.NewGuid():N}@test.com",
            password = "Test1234",
            displayName = "New"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"dup{Guid.NewGuid():N}@test.com";
        var payload = new { email, password = "Test1234", displayName = "Dup" };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        first.EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"wrong{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Test1234",
            displayName = "Wrong"
        });

        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "Nope1234"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsUser()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);

        var res = await client.GetAsync("/api/v1/auth/me");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();

        Assert.Contains("@test.com", body!.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task User_CannotSeeAnotherUsersWorkflow()
    {
        var alice = await ApiHelpers.LoginNewUserAsync(_factory);
        var bob = await ApiHelpers.LoginNewUserAsync(_factory);

        var workflowId = await ApiHelpers.CreateWorkflowAsync(alice);

        var res = await bob.GetAsync($"/api/v1/workflows/{workflowId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
