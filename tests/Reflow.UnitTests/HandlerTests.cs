using Reflow.Infrastructure.Data;
using Reflow.Infrastructure.Http;
using Reflow.Infrastructure.Storage;
using Reflow.Modules.DataProcessing;
using Xunit;

namespace Reflow.UnitTests;

public class HandlerTests
{
    private static TaskExecutionContext Ctx(string nodeId, string nodeType, string config, params Dataset[] inputs)
        => new(Guid.NewGuid(), Guid.NewGuid(), nodeId, nodeType, config, inputs, Guid.NewGuid(), null);

    private static FakeUploadStore Uploads() => new();

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
        var handler = new CsvReadHandler(Uploads());

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", """{"csvText":"a,b\n1,2\n3,4"}"""), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal(2, output.Quality.OutputCount);
    }

    [Fact]
    public async Task CsvRead_DedupesConfiguredColumns()
    {
        var handler = new CsvReadHandler(Uploads());

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", """{"csvText":"a\n1\n1\n2","dedupeColumns":["a"]}"""), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal(1, output.Quality.DuplicateCount);
    }

    [Fact]
    public async Task CsvRead_MissingText_Fails()
    {
        var handler = new CsvReadHandler(Uploads());

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

    [Fact]
    public void JsonNavigator_ResolvesNestedPaths()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{"data":{"orders":[{"id":1},{"id":2}]}}""");

        Assert.True(JsonNavigator.TryNavigate(doc.RootElement, "data.orders", out var target, out _));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, target.ValueKind);
        Assert.True(JsonNavigator.TryNavigate(doc.RootElement, "data.orders.1.id", out var id, out _));
        Assert.Equal(2, id.GetInt32());
        Assert.True(JsonNavigator.TryNavigate(doc.RootElement, null, out _, out _));
        Assert.False(JsonNavigator.TryNavigate(doc.RootElement, "data.missing", out _, out var error));
        Assert.Contains("missing", error);
        Assert.False(JsonNavigator.TryNavigate(doc.RootElement, "data.orders.9", out _, out _));
    }

    [Fact]
    public void JsonDataset_SelectsRootPath()
    {
        var dataset = JsonDataset.FromJsonText("""{"data":{"rows":[{"a":"1"}]}}""", "data.rows", "test");

        Assert.Equal(new[] { "a" }, dataset.Columns);
        Assert.Single(dataset.Rows);
    }

    [Fact]
    public async Task CsvRead_FromUpload()
    {
        var uploads = Uploads();
        var fileId = $"{new Guid():N}.csv".Replace("-", "");
        uploads.Add(fileId, "x,y\n1,2\n3,4");
        var handler = new CsvReadHandler(uploads);

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", $$"""{"source":"upload","fileId":"{{fileId}}"}"""), CancellationToken.None);

        Assert.Equal(2, OutputOf(result).Rows.Count);
    }

    [Fact]
    public async Task CsvRead_MissingUpload_Fails()
    {
        var handler = new CsvReadHandler(Uploads());

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read", """{"source":"upload","fileId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.csv"}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("no longer exists", result.Error);
    }

    [Fact]
    public async Task CsvRead_TrimSkipAndNulls()
    {
        var handler = new CsvReadHandler(Uploads());

        var result = await handler.ExecuteAsync(
            Ctx("read", "data.csv.read",
                """{"csvText":"a,b\nskip,me\n x ,NA\n1,2","skipRows":1,"nullValues":["NA"]}"""),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal("x", output.Rows[0][0]);
        Assert.Null(output.Rows[0][1]);
    }

    [Fact]
    public async Task JsonRead_ParsesTextAndRootPath()
    {
        var handler = new JsonReadHandler(Uploads());

        var result = await handler.ExecuteAsync(
            Ctx("j", "data.json.read", """{"jsonText":"{\"items\":[{\"a\":1},{\"a\":2}]}","rootPath":"items"}"""),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal("2", output.Rows[1][0]);
    }

    [Fact]
    public async Task JsonRead_FromColumn_MergesObjects()
    {
        var handler = new JsonReadHandler(Uploads());
        var input = Table(new[] { "id", "payload" }, new[] { "1", "{\"city\":\"Tunis\",\"zip\":\"1000\"}" }, new[] { "2", "not json" }, new[] { "3", null });

        var result = await handler.ExecuteAsync(
            Ctx("j", "data.json.read", """{"source":"input","column":"payload"}""", input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(new[] { "id", "payload", "city", "zip" }, output.Columns);
        Assert.Equal(1, output.Rows.Count);
        Assert.Equal("Tunis", output.Rows[0][2]);
        Assert.Equal(2, output.Quality.RejectedCount);
        Assert.Equal(1, output.Quality.FailuresByRule["json:parse"]);
        Assert.Equal(1, output.Quality.FailuresByRule["json:empty"]);
    }

    [Fact]
    public async Task JsonRead_FromColumn_ExplodesArrays()
    {
        var handler = new JsonReadHandler(Uploads());
        var input = Table(new[] { "id", "items" }, new[] { "1", "[{\"sku\":\"a\"},{\"sku\":\"b\"}]" });

        var result = await handler.ExecuteAsync(
            Ctx("j", "data.json.read", """{"source":"input","column":"items"}""", input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal("a", output.Rows[0][2]);
        Assert.Equal("1", output.Rows[1][0]);
    }

    [Fact]
    public async Task JsonRead_FromColumn_MissingColumn_Fails()
    {
        var handler = new JsonReadHandler(Uploads());
        var input = Table(new[] { "a" }, new[] { "1" });

        var result = await handler.ExecuteAsync(
            Ctx("j", "data.json.read", """{"source":"input","column":"ghost"}""", input),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ghost", result.Error);
    }

    [Fact]
    public async Task Validate_TypesAndUnique()
    {
        var handler = new ValidateHandler();
        var input = Table(new[] { "age", "code" }, new[] { "36", "A" }, new[] { "old", "B" }, new[] { "40", "A" });

        var result = await handler.ExecuteAsync(
            Ctx("val", "data.validate", """{"columnTypes":{"age":"integer"},"uniqueColumns":["code"]}""", input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Single(output.Rows);
        Assert.Equal(2, output.Quality.RejectedCount);
        Assert.Equal(1, output.Quality.FailuresByRule["type:age"]);
        Assert.Equal(1, output.Quality.FailuresByRule["unique:code"]);
    }

    [Theory]
    [InlineData("startsWith", "Ad", 1)]
    [InlineData("endsWith", "da", 1)]
    [InlineData("matches", "^A.*a$", 2)]
    [InlineData("inList", null, 2)]
    public async Task Filter_NewOperatorsWork(string op, string? value, int expected)
    {
        var handler = new FilterHandler();
        var input = Table(new[] { "name" }, new[] { "Ada" }, new[] { "Bob" }, new[] { "Ava" });
        var config = op == "inList"
            ? """{"column":"name","operator":"inList","value":["Ada","Ava"]}"""
            : $$"""{"column":"name","operator":"{{op}}","value":"{{value}}"}""";

        var result = await handler.ExecuteAsync(Ctx("flt", "data.filter", config, input), CancellationToken.None);

        Assert.Equal(expected, OutputOf(result).Rows.Count);
    }

    [Fact]
    public async Task Filter_BadPattern_Fails()
    {
        var handler = new FilterHandler();
        var input = Table(new[] { "x" }, new[] { "abc" });

        var result = await handler.ExecuteAsync(
            Ctx("flt", "data.filter", """{"column":"x","operator":"matches","value":"([unclosed"}""", input),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("pattern", result.Error);
    }

    [Fact]
    public async Task Transform_DropFillRoundConcat()
    {
        var handler = new TransformHandler();
        var input = Table(new[] { "a", "b", "c" }, new[] { "x", "10.567", null }, new[] { "y", "2", "z" });

        var result = await handler.ExecuteAsync(
            Ctx("tr", "data.transform",
                """{"dropColumns":["c"],"fillNull":{"b":"0"},"round":{"b":1},"concat":{"sources":["a","b"],"separator":"-","alias":"combo"}}""",
                input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(new[] { "a", "b", "combo" }, output.Columns);
        Assert.Equal("10.6", output.Rows[0][1]);
        Assert.Equal("x-10.6", output.Rows[0][2]);
    }

    [Fact]
    public async Task Aggregate_MedianAndDistinct()
    {
        var handler = new AggregateHandler();
        var input = Table(new[] { "g", "v" }, new[] { "a", "1" }, new[] { "a", "2" }, new[] { "a", "2" }, new[] { "b", "5" });

        var result = await handler.ExecuteAsync(
            Ctx("agg", "data.aggregate",
                """{"groupBy":["g"],"operations":[{"column":"v","operation":"median","alias":"med"},{"column":"v","operation":"countDistinct","alias":"d"}]}""",
                input),
            CancellationToken.None);

        var output = OutputOf(result);
        var rowA = output.Rows.Single(r => r[0] == "a");
        Assert.Equal("2", rowA[1]);
        Assert.Equal("2", rowA[2]);
    }

    [Fact]
    public async Task Sort_OrdersNumericallyThenLexically()
    {
        var handler = new SortHandler();
        var input = Table(new[] { "n", "s" }, new[] { "10", "b" }, new[] { "2", "a" }, new[] { "2", "c" });

        var result = await handler.ExecuteAsync(
            Ctx("sort", "data.sort", """{"orderBy":[{"column":"n","direction":"asc"},{"column":"s","direction":"desc"}]}""", input),
            CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal("c", output.Rows[0][1]);
        Assert.Equal("a", output.Rows[1][1]);
        Assert.Equal("b", output.Rows[2][1]);
    }

    [Fact]
    public async Task Limit_SkipsAndTakes()
    {
        var handler = new LimitHandler();
        var input = Table(new[] { "a" }, new[] { "1" }, new[] { "2" }, new[] { "3" });

        var result = await handler.ExecuteAsync(
            Ctx("lim", "data.limit", """{"offset":1,"count":1}""", input), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Single(output.Rows);
        Assert.Equal("2", output.Rows[0][0]);
    }

    [Fact]
    public async Task Dedupe_RemovesDuplicates()
    {
        var handler = new DedupeHandler();
        var input = Table(new[] { "a", "b" }, new[] { "1", "x" }, new[] { "1", "y" }, new[] { "1", "x" });

        var result = await handler.ExecuteAsync(
            Ctx("dd", "data.dedupe", """{"columns":["a","b"]}""", input), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        Assert.Equal(1, output.Quality.DuplicateCount);
    }

    [Fact]
    public async Task Join_InnerAndLeft()
    {
        var handler = new JoinHandler();
        var left = Table(new[] { "id", "name" }, new[] { "1", "Ada" }, new[] { "2", "Bob" });
        var right = Table(new[] { "id", "city" }, new[] { "1", "Tunis" }, new[] { "3", "Paris" });

        var inner = await handler.ExecuteAsync(
            Ctx("j", "data.join", """{"on":["id"],"how":"inner"}""", left, right), CancellationToken.None);
        var innerOut = OutputOf(inner);
        Assert.Single(innerOut.Rows);
        Assert.Equal(new[] { "id", "name", "city" }, innerOut.Columns);

        var outer = await handler.ExecuteAsync(
            Ctx("j", "data.join", """{"on":["id"],"how":"left"}""", left, right), CancellationToken.None);
        var outerOut = OutputOf(outer);
        Assert.Equal(2, outerOut.Rows.Count);
        Assert.Null(outerOut.Rows[1][2]);
    }

    [Fact]
    public async Task Join_RequiresTwoInputs()
    {
        var handler = new JoinHandler();
        var input = Table(new[] { "a" }, new[] { "1" });

        var result = await handler.ExecuteAsync(
            Ctx("j", "data.join", """{"on":["a"]}""", input), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("two inputs", result.Error);
    }

    [Fact]
    public async Task Profile_ComputesStats()
    {
        var handler = new ProfileHandler();
        var input = Table(new[] { "age", "name" }, new[] { "10", "Ada" }, new[] { "20", null }, new[] { "xx", "Ada" });

        var result = await handler.ExecuteAsync(
            Ctx("p", "data.profile", "{}", input), CancellationToken.None);

        var output = OutputOf(result);
        Assert.Equal(2, output.Rows.Count);
        var age = output.Rows.Single(r => r[0] == "age");
        Assert.Equal("3", age[1]);
        Assert.Equal("0", age[2]);
        Assert.Equal("3", age[3]);
        Assert.Equal("10", age[4]);
        Assert.Equal("20", age[5]);
        Assert.Equal("15", age[6]);
        var name = output.Rows.Single(r => r[0] == "name");
        Assert.Equal("1", name[2]);
        Assert.Equal("1", name[3]);
    }

    private sealed class FakeArtifactStore : IArtifactStore
    {
        public List<(string Format, string Content)> Saved { get; } = new();

        public Task<string> SaveAsync(Guid runId, string nodeId, Dataset dataset, string format, string? fileName, char delimiter, bool includeHeader, CancellationToken ct)
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

    private sealed class FakeUploadStore : IUploadStore
    {
        public Dictionary<string, string> Files { get; } = new();

        public void Add(string fileId, string content) => Files[fileId] = content;

        public Task<UploadedFile> SaveAsync(Guid workflowId, string fileName, Stream content, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<UploadedFile>> ListAsync(Guid workflowId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<UploadedFile>>(new List<UploadedFile>());

        public Task<bool> DeleteAsync(Guid workflowId, string fileId, CancellationToken ct)
            => Task.FromResult(Files.Remove(fileId));

        public Task<string?> ReadTextAsync(Guid workflowId, string fileId, CancellationToken ct)
            => Task.FromResult<string?>(Files.TryGetValue(fileId, out var text) ? text : null);

        public bool IsValidFileId(string fileId) => LocalUploadStore.IsValidFileId(fileId);
    }
}
