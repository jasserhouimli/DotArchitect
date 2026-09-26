using System.Text.Json;
using Reflow.Infrastructure.Expressions;
using Xunit;

namespace Reflow.UnitTests;

public class ExpressionTests
{
    private static readonly ExpressionContext Ctx = new(
        TriggerKind: "webhook",
        TriggerName: "Hook",
        TriggerPayloadJson: """{"order":{"id":"A-123","total":99.5},"tags":["x","y"],"active":true}""",
        RunId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        VersionNumber: 3);

    private static string Resolve(string config) =>
        ExpressionResolver.ResolveConfigJson(config, Ctx) switch
        {
            (true, var json) => json,
            (false, var error) => throw new Xunit.Sdk.XunitException($"Resolve failed: {error}")
        };

    private static string Fail(string config)
    {
        var (ok, value) = ExpressionResolver.ResolveConfigJson(config, Ctx);
        Assert.False(ok);
        return value;
    }

    private static JsonElement Json(string config) =>
        JsonDocument.Parse(Resolve(config)).RootElement;

    [Fact]
    public void WholeToken_PreservesNumberType()
    {
        var root = Json("""{"value":"{{ trigger.body.order.total }}"}""");
        Assert.Equal(JsonValueKind.Number, root.GetProperty("value").ValueKind);
        Assert.Equal(99.5, root.GetProperty("value").GetDouble());
    }

    [Fact]
    public void WholeToken_PreservesBoolAndObject()
    {
        var root = Json("""{"a":"{{ trigger.body.active }}","b":"{{ trigger.body.order }}"}""");
        Assert.Equal(JsonValueKind.True, root.GetProperty("a").ValueKind);
        Assert.Equal("A-123", root.GetProperty("b").GetProperty("id").GetString());
    }

    [Fact]
    public void EmbeddedToken_Stringifies()
    {
        var root = Json("""{"value":"order={{ trigger.body.order.id }}!"}""");
        Assert.Equal("order=A-123!", root.GetProperty("value").GetString());
    }

    [Fact]
    public void ArrayIndex_Navigates()
    {
        var root = Json("""{"value":"{{ trigger.body.tags.1 }}"}""");
        Assert.Equal("y", root.GetProperty("value").GetString());
    }

    [Fact]
    public void TriggerAndRun_Metadata()
    {
        var root = Json("""{"a":"{{ trigger.kind }}","b":"{{ trigger.name }}","c":"{{ run.id }}","d":"{{ run.version }}"}""");
        Assert.Equal("webhook", root.GetProperty("a").GetString());
        Assert.Equal("Hook", root.GetProperty("b").GetString());
        Assert.Equal("11111111-2222-3333-4444-555555555555", root.GetProperty("c").GetString());
        Assert.Equal(3, root.GetProperty("d").GetInt32());
    }

    [Fact]
    public void EscapedBraces_StayLiteral()
    {
        var root = Json("""{"value":"\\{{ not an expression }}"}""");
        Assert.Equal("{{ not an expression }}", root.GetProperty("value").GetString());
    }

    [Fact]
    public void NoTokens_PassthroughUntouched()
    {
        Assert.Equal(
            JsonDocument.Parse("""{"a":1,"b":true,"c":null,"d":"x"}""").RootElement.GetRawText(),
            JsonDocument.Parse(Resolve("""{"a":1,"b":true,"c":null,"d":"x"}""")).RootElement.GetRawText());
    }

    [Fact]
    public void MissingProperty_FailsClearly()
    {
        var error = Fail("""{"value":"{{ trigger.body.nope }}"}""");
        Assert.Contains("nope", error);
    }

    [Fact]
    public void UnknownRoot_FailsClearly()
    {
        var error = Fail("""{"value":"{{ nodes.x }}"}""");
        Assert.Contains("nodes", error);
    }

    [Fact]
    public void MissingPayload_FailsClearly()
    {
        var ctx = Ctx with { TriggerPayloadJson = null };
        var (ok, error) = ExpressionResolver.ResolveConfigJson("""{"v":"{{ trigger.body.x }}"}""", ctx);
        Assert.False(ok);
        Assert.Contains("payload", error);
    }

    [Fact]
    public void Validator_RejectsBadRootsAndUnbalanced()
    {
        var errors = ExpressionValidator.ValidateConfig("n1", """{"a":"{{ foo.bar }}","b":"{{ trigger.body.x }"}""").ToList();
        Assert.Contains(errors, e => e.Contains("'foo'"));
        Assert.Contains(errors, e => e.Contains("unbalanced"));

        Assert.Empty(ExpressionValidator.ValidateConfig("n1", """{"a":"{{ trigger.kind }}-{{ run.version }}"}"""));
        Assert.Empty(ExpressionValidator.ValidateConfig("n1", """{"a":"\\{{ literal }}"}"""));

        var unbalanced = ExpressionValidator.ValidateConfig("n1", """{"a":"price {{ trigger.kind"}""").ToList();
        Assert.Contains(unbalanced, e => e.Contains("unbalanced"));
    }
}
