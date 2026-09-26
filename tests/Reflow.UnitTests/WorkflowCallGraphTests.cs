using Reflow.Modules.WorkflowDesign.Validation;
using Xunit;

namespace Reflow.UnitTests;

public class WorkflowCallGraphTests
{
    private static IReadOnlyList<Guid> Outgoing(
        Dictionary<Guid, List<Guid>> edges, Guid id) =>
        edges.TryGetValue(id, out var list) ? list : Array.Empty<Guid>();

    [Fact]
    public void ExtractTargets_ParsesCallNodes()
    {
        var a = Guid.NewGuid();
        var nodes = new List<(string, string?)>
        {
            ("data.csv.read", """{"csvText":"a"}"""),
            ("workflow.call", $$"""{"targetWorkflowId":"{{a}}","mode":"wait"}"""),
            ("workflow.call", """{"mode":"wait"}"""),
            ("workflow.call", """not json"""),
            ("data.output", """{"format":"json"}""")
        };

        Assert.Equal(new[] { a }, WorkflowCallGraph.ExtractTargets(nodes));
    }

    [Fact]
    public void SelfCall_IsCycle()
    {
        var a = Guid.NewGuid();
        var edges = new Dictionary<Guid, List<Guid>>();
        Assert.True(WorkflowCallGraph.WouldCycle(a, new[] { a }, id => Outgoing(edges, id)));
    }

    [Fact]
    public void MutualCall_IsCycle()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var edges = new Dictionary<Guid, List<Guid>> { [b] = new() { a } };
        Assert.True(WorkflowCallGraph.WouldCycle(a, new[] { b }, id => Outgoing(edges, id)));
    }

    [Fact]
    public void ThreeCycle_IsCycle()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var edges = new Dictionary<Guid, List<Guid>>
        {
            [b] = new() { c },
            [c] = new() { a }
        };
        Assert.True(WorkflowCallGraph.WouldCycle(a, new[] { b }, id => Outgoing(edges, id)));
    }

    [Fact]
    public void Diamond_IsNotCycle()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        var edges = new Dictionary<Guid, List<Guid>>
        {
            [b] = new() { d },
            [c] = new() { d }
        };
        Assert.False(WorkflowCallGraph.WouldCycle(a, new[] { b, c }, id => Outgoing(edges, id)));
    }

    [Fact]
    public void NoCalls_IsNotCycle()
    {
        var a = Guid.NewGuid();
        Assert.False(WorkflowCallGraph.WouldCycle(a, Array.Empty<Guid>(), _ => Array.Empty<Guid>()));
    }
}
