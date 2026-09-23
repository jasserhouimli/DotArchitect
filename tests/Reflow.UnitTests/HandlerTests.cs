using Reflow.Modules.WorkflowExecution.Data;
using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Services;
using Xunit;

namespace Reflow.UnitTests;

public class HandlerTests
{
    private static TaskExecutionContext Ctx(string nodeId, string nodeType, string config, params Dataset[] inputs)
        => new(Guid.NewGuid(), Guid.NewGuid(), nodeId, nodeType, config, inputs);

    private static Dataset Table(params string[][] rows)
    {
        var dataset = new Dataset();
        dataset.Columns.AddRange(rows[0]);
        foreach (var row in rows.Skip(1))
            dataset.Rows.Add(row.Select(c => (string?)c).ToList());
        dataset.Quality.InputCount = dataset.Rows.Count;
        return dataset;
    }

    private static Dataset OutputOf(TaskExecutionResult result)
    {
        Assert.True(result.IsSuccess, result.Error);
        return Dataset.TryFromJson(result.OutputJson)!;
    }

    [Fact]
    public async Task CsvRead_ParsesRows()
    {
        var handler = new CsvReadHandler();

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", """{"csvText":"a,b\n1,2\n3,4"}"""), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal(2, output.Quality.OutputCount);
    }

    [Fact]
    public async Task CsvRead_DedupesConfiguredColumns()
    {
        var handler = new CsvReadHandler();

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", """{"csvText":"a\n1\n1\n2","dedupeColumns":["a"]}"""), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal(1, output.Quality.DuplicateCount);
    }

    [Fact]
    public async Task CsvRead_MissingText_Fails()
    {
        var handler = new CsvReadHandler();

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", "{}"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("csvText", result.Error);
    }

    [Fact]
    public async Task Validate_SplitsValidAndRejected()
    {
        var handler = new ValidateHandler();
        var input = Table(new[] { "name", "age" }, new[] { "Ada", "36" }, new[] { "", "40" }, new[] { "Bob", "" });

        var result = await handler.ExecuteAsync(
            Ctx("val", "data.validate", """{"requiredColumns":["name","age"]}""", input), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Single(output.Rows);
        Assert.Equal(2, output.Quality.RejectedCount);
        Assert.Equal(1, output.Quality.FailuresByRule["required:name"]);
        Assert.Equal(1, output.Quality.FailuresByRule["required:age"]);
    }

    [Fact]
    public async Task Validate_UnknownColumn_Fails()
    {
        var handler = new ValidateHandler();
        var input = Table(new[] { "a" }, new[] { "1" });

        var result = await handler.ExecuteAsync(
            Ctx("val", "data.validate", """{"requiredColumns":["ghost"]}""", input), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ghost", result.Error);
    }

    [Fact]
    public async Task Validate_WithoutInput_Fails()
    {
        var handler = new ValidateHandler();

        var result = await handler.ExecuteAsync(
            Ctx("val", "data.validate", "{}"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("no input", result.Error);
    }

    [Theory]
    [InlineData("equals", "2", 1)]
    [InlineData("notEquals", "2", 2)]
    [InlineData("contains", "2", 1)]
    [InlineData("greaterThan", "1", 1)]
    [InlineData("lessThan", "2", 2)]
    [InlineData("isEmpty", null, 1)]
    [InlineData("isNotEmpty", null, 2)]
    public async Task Filter_OperatorsWork(string op, string? value, int expected)
    {
        var handler = new FilterHandler();
        var input = Table(new[] { "x" }, new[] { "1" }, new[] { "2" }, new[] { "" });
        var valueJson = value is null ? "null" : $"\"{value}\"";
        var config = $$"""{"column":"x","operator":"{{op}}","value":{{valueJson}}}""";

        var result = await handler.ExecuteAsync(Ctx("flt", "data.filter", config, input), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(expected, output.Rows.Count);
    }

    [Fact]
    public async Task Filter_NonNumericComparison_DoesNotMatch()
    {
        var handler = new FilterHandler();
        var input = Table(new[] { "x" }, new[] { "abc" });

        var result = await handler.ExecuteAsync(
            Ctx("flt", "data.filter", """{"column":"x","operator":"greaterThan","value":"5"}""", input),
            CancellationToken.None);

        Assert.Empty(OutputOf(result).Rows);
    }

    [Fact]
    public async Task Transform_SelectRenameAndCase()
    {
        var handler = new TransformHandler();
        var input = Table(new[] { "a", "b" }, new[] { "x", "y" });

        var result = await handler.ExecuteAsync(
            Ctx("tr", "data.transform", """{"select":["a","b"],"renames":{"b":"B"},"upperColumns":["a"]}""", input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(new[] { "a", "B" }, output.Columns);
        Assert.Equal("X", output.Rows[0][0]);
        Assert.Equal("y", output.Rows[0][1]);
    }

    [Fact]
    public async Task Transform_UnknownColumn_Fails()
    {
        var handler = new TransformHandler();
        var input = Table(new[] { "a" }, new[] { "1" });

        var result = await handler.ExecuteAsync(
            Ctx("tr", "data.transform", """{"select":["ghost"]}""", input), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ghost", result.Error);
    }

    [Fact]
    public async Task Aggregate_GroupsAndSums()
    {
        var handler = new AggregateHandler();
        var input = Table(
            new[] { "region", "amount" },
            new[] { "east", "10" }, new[] { "east", "20" }, new[] { "west", "7" });

        var result = await handler.ExecuteAsync(
            Ctx("agg", "data.aggregate",
                """{"groupBy":["region"],"operations":[{"column":"amount","operation":"sum","alias":"total"},{"operation":"count","alias":"n"}]}""",
                input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(new[] { "region", "total", "n" }, output.Columns);
        Assert.Equal(2, output.Rows.Count);
        var east = output.Rows.Single(r => r[0] == "east");
        Assert.Equal("30", east[1]);
        Assert.Equal("2", east[2]);
    }

    [Fact]
    public async Task Aggregate_NonNumericColumn_Fails()
    {
        var handler = new AggregateHandler();
        var input = Table(new[] { "name" }, new[] { "Ada" });

        var result = await handler.ExecuteAsync(
            Ctx("agg", "data.aggregate", """{"operations":[{"column":"name","operation":"sum"}]}""", input),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("no numeric values", result.Error);
    }

    [Fact]
    public async Task Output_SavesArtifactViaStore()
    {
        var store = new FakeArtifactStore();
        var handler = new OutputHandler(store);
        var input = Table(new[] { "a" }, new[] { "1" });

        var result = await handler.ExecuteAsync(
            Ctx("out", "data.output", """{"format":"csv"}""", input), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(store.Saved);
        Assert.Equal("csv", store.Saved[0].Format);
        Assert.Contains("a", store.Saved[0].Content);
    }

    [Fact]
    public void HttpRequest_ParsesJsonArray()
    {
        var dataset = HttpRequestHandler.JsonToDataset(
            """[{"id":1,"name":"Ada"},{"id":2}]""", "http");

        Assert.NotNull(dataset);
        Assert.Equal(new[] { "id", "name" }, dataset!.Columns);
        Assert.Equal(2, dataset.Rows.Count);
        Assert.Null(dataset.Rows[1][1]);
    }

    [Fact]
    public void HttpRequest_ParsesSingleObject()
    {
        var dataset = HttpRequestHandler.JsonToDataset("""{"a":1}""", "http");

        Assert.NotNull(dataset);
        Assert.Single(dataset!.Rows);
    }

    [Fact]
    public void HttpRequest_RejectsNonJson()
    {
        Assert.Null(HttpRequestHandler.JsonToDataset("<html>nope</html>", "http"));
    }

    [Fact]
    public async Task SsrfGuard_BlocksDangerousUrls()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.AssertSafeAsync("ftp://example.com/x", false, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.AssertSafeAsync("http://127.0.0.1/x", false, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.AssertSafeAsync("http://10.0.0.5/x", false, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.AssertSafeAsync("http://192.168.1.1/x", false, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.AssertSafeAsync("https://user:pass@example.com/", false, CancellationToken.None));
    }

    [Fact]
    public async Task SsrfGuard_AllowsPublicIpLiteral()
    {
        await SsrfGuard.AssertSafeAsync("https://8.8.8.8/dns", false, CancellationToken.None);
    }

    private sealed class FakeArtifactStore : IArtifactStore
    {
        public List<(string Format, string Content)> Saved { get; } = new();

        public Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, CancellationToken ct)
        {
            var content = format == "csv" ? "csv-stub" : dataset.ToJson();
            if (format == "csv")
                content = string.Join("\n", dataset.Columns) + "\n" + string.Join("\n", dataset.Rows.Select(r => string.Join(",", r)));
            Saved.Add((format, content));
            return Task.FromResult("fake." + format);
        }

        public Task<(string Content, string ContentType, string FileName)?> LoadAsync(Guid runId, string nodeId, CancellationToken ct)
            => Task.FromResult<(string, string, string)?>(null);
    }
}
