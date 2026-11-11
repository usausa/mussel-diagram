namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class QueryTests
{
    [Fact]
    public void LinksAreFollowedBothWays()
    {
        var snapshot = CreateFlow().Snapshot;

        Assert.Equal(["b"], snapshot.GetPredecessors("c").Select(x => x.Id));
        Assert.Equal(["a", "d"], snapshot.GetSuccessors("c").Select(x => x.Id));
        Assert.Equal(["b", "c", "d"], snapshot.GetDownstream("a").Select(x => x.Id));
        Assert.Equal(["c", "b", "a"], snapshot.GetUpstream("d").Select(x => x.Id));
        Assert.Empty(snapshot.GetSuccessors("d"));
        Assert.Empty(snapshot.GetUpstream("missing"));
    }

    [Fact]
    public void CyclesAndUnreachableElementsAreFound()
    {
        var snapshot = CreateFlow().Snapshot;

        Assert.Equal(["a", "b", "c", "e"], snapshot.Cycles.Select(x => x.Id));
        Assert.Equal(["a", "b", "c", "e"], snapshot.GetUnreachable(["d"]).Select(x => x.Id));
        Assert.Equal(["e"], snapshot.GetUnreachable(["a"]).Select(x => x.Id));
    }

    [Fact]
    public void OverlayMembersAreFoundBothWays()
    {
        var snapshot = CreateFlow().Snapshot;

        Assert.Equal(["a", "b"], snapshot.GetMembers("asg").Select(x => x.Id));
        Assert.Equal(["asg"], snapshot.GetOverlayGroups("a").Select(x => x.Id));
        Assert.Empty(snapshot.GetOverlayGroups("c"));
        Assert.Empty(snapshot.GetMembers("zone"));
    }

    private static Diagram CreateFlow()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("zone", 0, 600, 300, 200));
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 200, 0));
        diagram.Add(Node("c", 400, 0));
        diagram.Add(Node("d", 600, 0));
        diagram.Add(Node("e", 800, 0));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Connect(Link("bc", "b", "c"));
        diagram.Connect(Link("ca", "c", "a"));
        diagram.Connect(Link("cd", "c", "d"));
        diagram.Connect(Link("ee", "e", "e"));
        diagram.Add(new DiagramGroup { Id = "asg", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["a", "b"] });
        return diagram;
    }
}
