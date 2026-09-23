using Reflow.Modules.WorkflowExecution.Data;
using Xunit;

namespace Reflow.UnitTests;

public class CsvParserTests
{
    [Fact]
    public void ParsesHeaderAndRows()
    {
        var dataset = CsvParser.Parse("name,age\nAda,36\nGrace,85", ',', true);

        Assert.Equal(new[] { "name", "age" }, dataset.Columns);
        Assert.Equal(2, dataset.Rows.Count);
        Assert.Equal("Ada", dataset.Rows[0][0]);
        Assert.Equal("85", dataset.Rows[1][1]);
    }

    [Fact]
    public void HandlesQuotedCommasAndEscapedQuotes()
    {
        var dataset = CsvParser.Parse("a,b\n\"x,y\",\"say \"\"hi\"\"\"", ',', true);

        Assert.Equal("x,y", dataset.Rows[0][0]);
        Assert.Equal("say \"hi\"", dataset.Rows[0][1]);
    }

    [Fact]
    public void HandlesMultilineQuotedField()
    {
        var dataset = CsvParser.Parse("a,b\n\"line1\nline2\",2", ',', true);

        Assert.Equal("line1\nline2", dataset.Rows[0][0]);
    }

    [Fact]
    public void SupportsCustomDelimiter()
    {
        var dataset = CsvParser.Parse("a;b\n1;2", ';', true);

        Assert.Equal(new[] { "a", "b" }, dataset.Columns);
        Assert.Equal("2", dataset.Rows[0][1]);
    }

    [Fact]
    public void GeneratesColumnNamesWithoutHeader()
    {
        var dataset = CsvParser.Parse("1,2\n3,4", ',', false);

        Assert.Equal(new[] { "col1", "col2" }, dataset.Columns);
        Assert.Equal(2, dataset.Rows.Count);
    }

    [Fact]
    public void SkipsBlankLines()
    {
        var dataset = CsvParser.Parse("a\n1\n\n2\n", ',', true);

        Assert.Equal(2, dataset.Rows.Count);
    }

    [Fact]
    public void ShortRows_ArePaddedWithNull()
    {
        var dataset = CsvParser.Parse("a,b,c\n1,2", ',', true);

        Assert.Equal(new List<string?> { "1", "2", null }, dataset.Rows[0]);
    }

    [Fact]
    public void DuplicateHeader_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse("a,A\n1,2", ',', true));
    }

    [Fact]
    public void UnterminatedQuote_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse("a\n\"oops", ',', true));
    }

    [Fact]
    public void EmptyInput_YieldsEmptyDataset()
    {
        var dataset = CsvParser.Parse(string.Empty, ',', true);

        Assert.Empty(dataset.Columns);
        Assert.Empty(dataset.Rows);
    }
}

public class DatasetTests
{
    [Fact]
    public void Merge_UnionsColumnsAndPadsMissing()
    {
        var left = new Dataset
        {
            Columns = new List<string> { "a", "b" },
            Rows = new List<List<string?>> { new() { "1", "2" } }
        };
        var right = new Dataset
        {
            Columns = new List<string> { "b", "c" },
            Rows = new List<List<string?>> { new() { "3", "4" } }
        };

        var merged = Dataset.Merge(new[] { left, right });

        Assert.Equal(new[] { "a", "b", "c" }, merged.Columns);
        Assert.Equal(new List<string?> { "1", "2", null }, merged.Rows[0]);
        Assert.Equal(new List<string?> { null, "3", "4" }, merged.Rows[1]);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var dataset = new Dataset
        {
            Columns = new List<string> { "a" },
            Rows = new List<List<string?>> { new() { "x" }, new() { null } },
            Quality = new DatasetQuality { InputCount = 2, OutputCount = 2, RejectedCount = 0 }
        };

        var restored = Dataset.TryFromJson(dataset.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(dataset.Columns, restored!.Columns);
        Assert.Equal(2, restored.Rows.Count);
        Assert.Null(restored.Rows[1][0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void TryFromJson_RejectsNonObjects(string? json)
    {
        Assert.Null(Dataset.TryFromJson(json));
    }
}
