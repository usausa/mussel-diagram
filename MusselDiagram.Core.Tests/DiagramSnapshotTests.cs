namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class DiagramSnapshotTests
{
    [Fact]
    public void TreeQueriesFollowTheParents()
    {
        var snapshot = CreateDiagram().Snapshot;

        Assert.Equal(["vpc", "c"], snapshot.TopLevel.Select(x => x.Id));
        Assert.Equal(["subnet", "b"], snapshot.GetChildren("vpc").Select(x => x.Id));
        Assert.Equal(["a", "b", "subnet"], snapshot.GetDescendants("vpc").Select(x => x.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["subnet", "vpc"], snapshot.GetAncestors("a").Select(x => x.Id));
        Assert.Equal("subnet", snapshot.GetParent("a")!.Id);
        Assert.Null(snapshot.GetParent("vpc"));
        Assert.Empty(snapshot.GetChildren("a"));
    }

    [Fact]
    public void LinksAreFoundByDirectionAndPort()
    {
        var snapshot = CreateDiagram().Snapshot;

        Assert.Equal(["ab", "ca"], snapshot.GetLinks("a").Select(x => x.Id));
        Assert.Equal(["ab"], snapshot.GetLinks("a", DiagramLinkDirection.Out).Select(x => x.Id));
        Assert.Equal(["ca"], snapshot.GetLinks("a", DiagramLinkDirection.In).Select(x => x.Id));
        Assert.Equal(["ca"], snapshot.GetLinks("c", DiagramLinkDirection.Both, "out").Select(x => x.Id));
        Assert.Empty(snapshot.GetLinks("c", DiagramLinkDirection.Both, "in"));
        Assert.Empty(snapshot.GetLinks("missing"));
    }

    [Fact]
    public void SnapshotsShareUnchangedElements()
    {
        var diagram = CreateDiagram();
        var before = diagram.Snapshot;

        diagram.Move(["c"], new DiagramPoint(5, 0));

        Assert.Same(before.Find("a"), diagram.Snapshot.Find("a"));
        Assert.NotSame(before.Find("c"), diagram.Snapshot.Find("c"));
        Assert.Equal(before.Version + 1, diagram.Snapshot.Version);
        Assert.True(diagram.Snapshot.Contains("c"));
        Assert.False(diagram.Snapshot.Contains("missing"));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Group("subnet", 20, 20, 300, 200, "vpc"));
        diagram.Add(Node("a", 40, 40, "subnet"));
        diagram.Add(Node("b", 400, 40, "vpc"));
        diagram.Add(Node("c", 800, 40, type: "service"));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Connect(Link("ca", "c", "a", sourcePort: "out"));
        return diagram;
    }
}
