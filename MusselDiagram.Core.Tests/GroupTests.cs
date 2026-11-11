namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class GroupTests
{
    private static readonly DiagramSchema GroupSchema = Schema with
    {
        GroupTypes =
        [
            new DiagramGroupType { Name = "vpc" },
            new DiagramGroupType { Name = "fit", Sizing = DiagramGroupSizing.Content, Padding = new DiagramThickness(10, 30, 10, 10), Size = new DiagramSize(200, 100) },
            new DiagramGroupType { Name = "scaling", Kind = DiagramGroupKind.Overlay, Padding = new DiagramThickness(5, 5, 5, 5) }
        ]
    };

    [Fact]
    public void OverlayGroupSurroundsItsMembersWithThePadding()
    {
        var diagram = new Diagram(GroupSchema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 200));
        diagram.Add(Overlay("asg", "a", "b"));
        Assert.Equal(new DiagramRect(-5, -5, 410, 260), Bounds(diagram, "asg"));

        diagram.Move(["b"], new DiagramPoint(100, 0));
        Assert.Equal(new DiagramRect(-5, -5, 510, 260), Bounds(diagram, "asg"));

        diagram.SetMembers("asg", ["a"]);
        Assert.Equal(new DiagramRect(-5, -5, 110, 60), Bounds(diagram, "asg"));

        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;
        diagram.SetMembers("asg", []);
        Assert.False(diagram.Snapshot.Contains("asg"));
        Assert.Equal(["asg"], change!.Deleted);
    }

    [Fact]
    public void ContentGroupFitsItsChildren()
    {
        var diagram = new Diagram(GroupSchema);
        diagram.Add(new DiagramGroup { Id = "fit", Type = "fit", Bounds = new DiagramRect(0, 0, 50, 50) });
        Assert.Equal(new DiagramRect(0, 0, 200, 100), Bounds(diagram, "fit"));

        diagram.Add(Node("a", 100, 100, "fit"));
        Assert.Equal(new DiagramRect(90, 70, 120, 90), Bounds(diagram, "fit"));

        diagram.Move(["a"], new DiagramPoint(50, 0));
        Assert.Equal(new DiagramRect(140, 70, 120, 90), Bounds(diagram, "fit"));

        diagram.Delete(["a"]);
        Assert.Equal(new DiagramRect(140, 70, 200, 100), Bounds(diagram, "fit"));
    }

    [Fact]
    public void ManualGroupGrowsButNeverShrinks()
    {
        var diagram = new Diagram(GroupSchema);
        diagram.Add(Group("outer", 0, 0, 800, 300));
        diagram.Add(Group("vpc", 20, 40, 300, 200, "outer"));
        diagram.Add(Node("a", 40, 80, "vpc"));
        Assert.Equal(new DiagramRect(20, 40, 300, 200), Bounds(diagram, "vpc"));

        diagram.Move(["a"], new DiagramPoint(300, 0));
        Assert.Equal(new DiagramRect(20, 40, 440, 200), Bounds(diagram, "vpc"));
        Assert.Equal(new DiagramRect(0, 0, 800, 300), Bounds(diagram, "outer"));

        diagram.Move(["a"], new DiagramPoint(0, 200));
        Assert.Equal(new DiagramRect(20, 40, 440, 310), Bounds(diagram, "vpc"));
        Assert.Equal(new DiagramRect(0, 0, 800, 370), Bounds(diagram, "outer"));

        diagram.Move(["a"], new DiagramPoint(-300, -200));
        Assert.Equal(new DiagramRect(20, 40, 440, 310), Bounds(diagram, "vpc"));
    }

    [Fact]
    public void SetParentKeepsParentsBeforeChildren()
    {
        var diagram = new Diagram(GroupSchema);
        diagram.Add(Group("b", 700, 40, 300, 300));
        diagram.Add(Group("a", 0, 0, 600, 400));
        diagram.Add(Node("n", 40, 80, "a"));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.SetParent(["b"], "a");

        Assert.Equal(["a", "b"], diagram.Snapshot.Groups.Select(x => x.Id));
        Assert.Equal("a", ((DiagramBox)diagram.Find("b")!).ParentId);
        Assert.Equal(DiagramChangeParts.Parent | DiagramChangeParts.Order, change!.GetChanges("b"));
        Assert.Equal(new DiagramRect(0, 0, 1020, 400), Bounds(diagram, "a"));

        Assert.Throws<DiagramException>(() => diagram.SetParent(["a"], "b"));
        Assert.Throws<DiagramException>(() => diagram.SetParent(["n"], "n"));

        diagram.SetParent(["n", "b"], null);
        Assert.Equal(["b", "n"], diagram.Snapshot.TopLevel.Select(x => x.Id).Where(x => x != "a"));
    }

    [Fact]
    public void KindsAndMembersAreChecked()
    {
        var diagram = new Diagram(GroupSchema);
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Node("a", 40, 80, "vpc"));
        diagram.Add(Node("b", 200, 80, "vpc"));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Add(Overlay("asg", "a"));
        var before = diagram.Snapshot;

        Assert.Throws<DiagramException>(() => diagram.Replace((DiagramGroup)diagram.Find("vpc")! with { Kind = DiagramGroupKind.Overlay }));
        Assert.Throws<DiagramException>(() => diagram.SetMembers("vpc", ["a"]));
        Assert.Throws<DiagramException>(() => diagram.SetMembers("asg", ["ab"]));
        Assert.Throws<DiagramException>(() => diagram.SetMembers("asg", ["asg"]));
        Assert.Throws<DiagramException>(() => diagram.SetParent(["asg"], "vpc"));
        Assert.Throws<DiagramException>(() => diagram.Add(Overlay("empty")));
        Assert.Same(before, diagram.Snapshot);
    }

    private static DiagramGroup Overlay(string id, params string[] members) =>
        new() { Id = id, Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = [.. members] };

    private static DiagramRect Bounds(Diagram diagram, string id) => ((DiagramBox)diagram.Find(id)!).Bounds;
}
