using Reflow.Modules.WorkflowDesign.Validation;
using Xunit;

namespace Reflow.UnitTests;

public class WorkflowValidatorTests
{
    private static NodeInput Node(string id, string type, string? config = "{}") => new(id, type, config);
    private static EdgeInput Edge(string from, string to) => new(from, to);

    [Fact]
    public void EmptyWorkflow_IsInvalid()
    {
        var result = WorkflowValidator.Validate([], []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no nodes"));
    }

    [Fact]
    public void ValidChain_Passes()
    {
        var nodes = new[]
        {
            Node("read", "data.csv.read", """{"csvText":"a,b\n1,2"}"""),
            Node("out", "data.output", """{"format":"json"}"""),
        };
        var edges = new[] { Edge("read", "out") };

        var result = WorkflowValidator.Validate(nodes, edges);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void UnsupportedNodeType_IsError()
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", "python.exec") }, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unsupported type"));
    }

    [Fact]
    public void DuplicateNodeIds_AreRejected()
    {
        var result = WorkflowValidator.Validate(
            new[] { Node("n1", "data.output"), Node("n1", "data.output") }, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate node ID"));
    }

    [Fact]
    public void Cycle_IsRejected()
    {
        var nodes = new[] { Node("a", "data.output"), Node("b", "data.output") };
        var edges = new[] { Edge("a", "b"), Edge("b", "a") };

        var result = WorkflowValidator.Validate(nodes, edges);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cycle"));
    }

    [Fact]
    public void DanglingEdge_IsRejected()
    {
        var result = WorkflowValidator.Validate(
            new[] { Node("a", "data.output") }, new[] { Edge("a", "ghost") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown target"));
    }

    [Fact]
    public void SelfEdge_IsRejected()
    {
        var result = WorkflowValidator.Validate(
            new[] { Node("a", "data.output") }, new[] { Edge("a", "a") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Self-referencing"));
    }

    [Fact]
    public void TooManyNodes_AreRejected()
    {
        var nodes = Enumerable.Range(0, WorkflowValidator.MaxNodes + 1)
            .Select(i => Node($"n{i}", "data.output"))
            .ToList();

        var result = WorkflowValidator.Validate(nodes, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maximum"));
    }

    [Fact]
    public void InvalidConfigJson_IsError()
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", "data.output", "{oops") }, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not valid JSON"));
    }

    [Theory]
    [InlineData("data.csv.read", "{}", "csvText")]
    [InlineData("http.request", "{}", "url")]
    [InlineData("http.request", """{"url":"ftp://x/y"}""", "http(s)")]
    [InlineData("data.filter", """{"column":"a","operator":"maybe"}""", "operator")]
    [InlineData("data.filter", """{"column":"a","operator":"equals"}""", "'value'")]
    [InlineData("data.aggregate", "{}", "operations")]
    [InlineData("data.aggregate", """{"operations":[{"operation":"sum"}]}""", "'column'")]
    [InlineData("data.output", """{"format":"xml"}""", "format")]
    public void RequiredConfigRules_ProduceErrors(string type, string config, string expectedFragment)
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", type, config) }, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("data.csv.read", """{"csvText":"a\n1","delimiter":",","hasHeader":true}""")]
    [InlineData("http.request", """{"url":"https://api.example.com/x","timeoutSeconds":10}""")]
    [InlineData("data.validate", """{"requiredColumns":["a"]}""")]
    [InlineData("data.filter", """{"column":"a","operator":"isEmpty"}""")]
    [InlineData("data.transform", """{"select":["a"],"renames":{"b":"c"}}""")]
    [InlineData("data.aggregate", """{"groupBy":["g"],"operations":[{"column":"v","operation":"sum","alias":"t"}]}""")]
    [InlineData("data.output", """{"format":"csv"}""")]
    public void WellFormedConfigs_Pass(string type, string config)
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", type, config) }, []);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NodeTypes_AreCaseInsensitive()
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", "DATA.OUTPUT") }, []);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("data.json.read", "{}", "jsonText")]
    [InlineData("data.json.read", """{"source":"input"}""", "'column'")]
    [InlineData("data.csv.read", """{"source":"upload"}""", "fileId")]
    [InlineData("data.sort", "{}", "orderBy")]
    [InlineData("data.limit", "{}", "count")]
    [InlineData("data.join", "{}", "leftOn")]
    [InlineData("data.join", """{"on":[]}""", "'on'")]
    [InlineData("data.filter", """{"column":"a","operator":"inList","value":"x"}""", "non-empty array")]
    [InlineData("data.transform", """{"select":["a"],"dropColumns":["b"]}""", "cannot combine")]
    [InlineData("http.request", """{"url":"https://x.test","headers":{"authorization":"t"}}""", "not allowed")]
    [InlineData("http.request", """{"url":"https://x.test","pagination":{"maxPages":99}}""", "maxPages")]
    [InlineData("data.output", """{"fileName":"../evil"}""", "safe file name")]
    public void NewConfigRules_ProduceErrors(string type, string config, string expectedFragment)
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", type, config) }, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("data.json.read", """{"jsonText":"[1]","rootPath":"a.b.0"}""")]
    [InlineData("data.json.read", """{"source":"input","column":"payload","rootPath":"rows"}""")]
    [InlineData("data.sort", """{"orderBy":[{"column":"a","direction":"desc"}]}""")]
    [InlineData("data.limit", """{"offset":5,"count":10}""")]
    [InlineData("data.dedupe", """{"columns":["a"]}""")]
    [InlineData("data.join", """{"on":["id"],"how":"left"}""")]
    [InlineData("data.join", """{"leftOn":["a"],"rightOn":["b"]}""")]
    [InlineData("data.profile", "{}")]
    [InlineData("data.validate", """{"columnTypes":{"n":"integer"},"uniqueColumns":["id"]}""")]
    [InlineData("data.transform", """{"dropColumns":["x"],"fillNull":{"y":"0"},"round":{"n":2}}""")]
    [InlineData("data.output", """{"format":"csv","fileName":"report","delimiter":";"}""")]
    public void NewWellFormedConfigs_Pass(string type, string config)
    {
        var result = WorkflowValidator.Validate(new[] { Node("n1", type, config) }, []);

        Assert.True(result.IsValid);
    }
}
