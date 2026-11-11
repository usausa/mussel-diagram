namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class DiagramWriteTests
{
    [Fact]
    public void AddGivesAnIdAndRaisesOneChange()
    {
        var diagram = new Diagram(Schema);
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);
        var tag = new object();

        diagram.Add(new DiagramNode { Type = "server", Bounds = new DiagramRect(0, 0, 100, 50) }, new DiagramWriteOptions { Tag = tag });

        var node = Assert.Single(diagram.Snapshot.Nodes);
        Assert.Matches("^[0-9a-f]{16}$", node.Id);
        Assert.Equal(1, diagram.Snapshot.Version);
        var change = Assert.Single(events);
        Assert.Equal([node.Id], change.Added);
        Assert.Empty(change.Updated);
        Assert.Equal(DiagramChangeOrigin.Api, change.Origin);
        Assert.Same(tag, change.Tag);
        Assert.Same(diagram.Snapshot, change.After);
        Assert.Equal(0, change.Before.Version);
    }

    [Fact]
    public void BrokenWritesThrowAndKeepTheSnapshot()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("vpc", 0, 0, 400, 300));
        diagram.Add(Node("a", 10, 10, "vpc"));
        diagram.Add(new DiagramGroup { Id = "asg", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["a"] });
        var before = diagram.Snapshot;

        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0)));
        Assert.Throws<DiagramException>(() => diagram.Add(Node(string.Empty, 0, 0)));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0) with { Bounds = new DiagramRect(0, 0, 0, 50) }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0) with { Bounds = new DiagramRect(double.NaN, 0, 10, 10) }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0, "missing")));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0, "a")));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0, "asg")));
        Assert.Throws<DiagramException>(() => diagram.Add(Group("g", 0, 0, 10, 10) with { Kind = DiagramGroupKind.Overlay }));
        Assert.Throws<DiagramException>(() => diagram.Add(Group("g", 0, 0, 10, 10) with { MemberIds = ["a"] }));
        Assert.Throws<DiagramException>(() => diagram.Add(new DiagramGroup { Id = "g", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["vpc", "asg"] }));
        Assert.Throws<DiagramException>(() => diagram.Add(Link("l", "a", "missing")));
        Assert.Throws<DiagramException>(() => diagram.Add(Link("l", "a", "a", sourcePort: "none")));
        Assert.Throws<DiagramException>(() => diagram.Replace(Group("vpc", 0, 0, 400, 300, "vpc")));
        Assert.Throws<DiagramException>(() => diagram.Replace(Group("a", 0, 0, 10, 10)));
        Assert.Throws<DiagramException>(() => diagram.Replace(Node("missing", 0, 0)));

        Assert.Same(before, diagram.Snapshot);
    }

    [Fact]
    public void PortsAreCheckedOnlyForRegisteredTypes()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0, type: "custom"));
        diagram.Add(Node("b", 300, 0, type: "service"));

        diagram.Connect(Link("l1", "a", "b", sourcePort: "anything", targetPort: "in"));

        Assert.Single(diagram.Snapshot.Links);
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("l2", "a", "b", targetPort: "anything")));
    }

    [Fact]
    public void MoveTakesDescendantsAndReroutesLinks()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Group("subnet", 20, 40, 300, 200, "vpc"));
        diagram.Add(Node("a", 40, 80, "subnet"));
        diagram.Add(Node("b", 400, 40, "vpc"));
        diagram.Add(Node("c", 800, 40));
        diagram.Connect(Link("ac", "a", "c"));
        diagram.Connect(Link("bc", "b", "c"));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Move(["subnet"], new DiagramPoint(10, 5));

        Assert.Equal(new DiagramRect(30, 45, 300, 200), ((DiagramBox)diagram.Find("subnet")!).Bounds);
        Assert.Equal(new DiagramRect(50, 85, 100, 50), ((DiagramBox)diagram.Find("a")!).Bounds);
        Assert.Equal(new DiagramRect(0, 0, 600, 400), ((DiagramBox)diagram.Find("vpc")!).Bounds);
        Assert.NotNull(change);
        Assert.Equal(DiagramChangeParts.Position, change.GetChanges("a"));
        Assert.Equal(DiagramChangeParts.Route, change.GetChanges("ac"));
        Assert.Equal(DiagramChangeParts.None, change.GetChanges("bc"));
        Assert.Equal(new DiagramPoint(150, 110), ((DiagramLink)diagram.Find("ac")!).Route[0]);
    }

    [Fact]
    public void MovingAnOverlayGroupMovesItsMembersButNotTheirParents()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Node("a", 40, 40, "vpc"));
        diagram.Add(Node("b", 400, 40, "vpc"));
        diagram.Add(new DiagramGroup { Id = "asg", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["a", "b"] });

        diagram.Move(["asg"], new DiagramPoint(0, 100));

        Assert.Equal(140, ((DiagramBox)diagram.Find("a")!).Bounds.Y);
        Assert.Equal(140, ((DiagramBox)diagram.Find("b")!).Bounds.Y);
        Assert.Equal(0, ((DiagramBox)diagram.Find("vpc")!).Bounds.Y);
    }

    [Fact]
    public void MoveWithoutEffectMakesNoVersion()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        var count = 0;
        diagram.Changed += (_, _) => count++;

        diagram.Move(["a"], default);
        diagram.Move(["missing"], new DiagramPoint(1, 1));
        diagram.Replace(Node("a", 0, 0));

        Assert.Equal(0, count);
        Assert.Equal(1, diagram.Snapshot.Version);
    }

    [Fact]
    public void DeleteTakesDescendantsLinksAndEmptyOverlayGroups()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Group("subnet", 20, 20, 300, 200, "vpc"));
        diagram.Add(Node("a", 40, 40, "subnet"));
        diagram.Add(Node("b", 800, 40));
        diagram.Add(Node("c", 800, 400));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Add(new DiagramGroup { Id = "only-a", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["a"] });
        diagram.Add(new DiagramGroup { Id = "a-and-b", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["a", "b"] });
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Delete(["vpc"]);

        Assert.Equal(["a-and-b"], diagram.Snapshot.Groups.Select(x => x.Id));
        Assert.Equal(["b", "c"], diagram.Snapshot.Nodes.Select(x => x.Id));
        Assert.Empty(diagram.Snapshot.Links);
        Assert.Equal(["b"], ((DiagramGroup)diagram.Find("a-and-b")!).MemberIds);
        Assert.NotNull(change);
        Assert.Equal(["a", "ab", "only-a", "subnet", "vpc"], change.Deleted.Order(StringComparer.Ordinal));
        Assert.Equal(DiagramChangeParts.Position | DiagramChangeParts.Size | DiagramChangeParts.Members, change.GetChanges("a-and-b"));
    }

    [Fact]
    public void ReplaceReportsTheChangedParts()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 0));
        diagram.Connect(Link("ab", "a", "b"));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Replace(Node("a", 0, 0) with { Label = "Web" });
        Assert.Equal(DiagramChangeParts.Label, change!.GetChanges("a"));
        Assert.DoesNotContain("ab", change.Updated);

        diagram.Replace(Node("a", 0, 100) with { Label = "Web" });
        Assert.Equal(DiagramChangeParts.Position, change.GetChanges("a"));
        Assert.Equal(DiagramChangeParts.Route, change.GetChanges("ab"));

        diagram.Connect(Link("ba", "b", "a"));
        diagram.Replace(Link("ba", "b", "b"));
        Assert.Equal(DiagramChangeParts.Ends | DiagramChangeParts.Route, change.GetChanges("ba"));
    }
}
