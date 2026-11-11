namespace MusselDiagram;

using System.Text.Json;

using MusselDiagram.Internal;

using static MusselDiagram.TestDiagrams;

public sealed class EditingTests
{
    [Fact]
    public void OrderMovesElementsWithinTheirLayer()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 0, 100));
        diagram.Add(Node("c", 0, 200));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.BringToFront(["a"]);

        Assert.Equal(["b", "c", "a"], Ids(diagram.Snapshot.Nodes));
        Assert.True(change!.GetChanges("a").HasFlag(DiagramChangeParts.Order));

        diagram.SendToBack(["a"]);
        Assert.Equal(["a", "b", "c"], Ids(diagram.Snapshot.Nodes));
        diagram.BringForward(["a", "b"]);
        Assert.Equal(["c", "a", "b"], Ids(diagram.Snapshot.Nodes));
        diagram.SendBackward(["b"]);
        Assert.Equal(["c", "b", "a"], Ids(diagram.Snapshot.Nodes));
    }

    [Fact]
    public void TreeGroupsMoveAmongTheirSiblings()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("v1", 0, 0, 600, 400));
        diagram.Add(Group("s1", 20, 40, 200, 200, "v1"));
        diagram.Add(Group("s2", 300, 40, 200, 200, "v1"));
        diagram.Add(Group("v2", 700, 0, 300, 300));

        diagram.BringToFront(["s1"]);

        Assert.Equal(["v1", "s2", "s1", "v2"], Ids(diagram.Snapshot.Groups));

        diagram.SendToBack(["v2"]);

        Assert.Equal(["v2", "v1", "s2", "s1"], Ids(diagram.Snapshot.Groups));
    }

    [Fact]
    public void LockedAndUndeletableElementsAreNotDeleted()
    {
        var schema = Schema with { NodeTypes = [.. Schema.NodeTypes, new DiagramNodeType { Name = "fixed", Undeletable = true }] };
        var diagram = new Diagram(schema);
        diagram.Add(Group("g", 0, 0, 400, 300));
        diagram.Add(Node("a", 20, 40, "g") with { Locked = true });
        diagram.Add(Node("b", 500, 0));
        diagram.Add(Node("c", 500, 100, type: "fixed"));
        diagram.Add(Node("d", 500, 200));
        diagram.Connect(Link("bd", "b", "d"));

        var deletable = Deletion.Deletable(diagram.Snapshot, diagram.Index, ["g", "a", "b", "c"]);

        Assert.Equal(["b"], deletable);
        Assert.Equal(["b", "bd"], Deletion.Removed(diagram.Snapshot, diagram.Index, deletable).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CopyTakesDescendantsInnerLinksAndSharedItems()
    {
        var schema = Schema with
        {
            NodeTypes = [.. Schema.NodeTypes, new DiagramNodeType { Name = "user", GetReferences = static data => data is JsonElement { ValueKind: JsonValueKind.String } id ? [id.GetString()!] : [] }],
            SharedItemTypes = [new DiagramSharedItemType { Name = "role" }]
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramSharedItem { Id = "admin", Type = "role" });
        diagram.Add(Group("g", 0, 0, 400, 300));
        diagram.Add(Node("a", 20, 40, "g"));
        diagram.Add(Node("b", 200, 40, "g", "user") with { Data = JsonDocument.Parse("\"admin\"").RootElement });
        diagram.Add(Node("c", 500, 0));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Connect(Link("ac", "a", "c"));

        var content = Clipboard.Copy(diagram.Snapshot, diagram.Index, ["g"])!;

        Assert.Equal(["g", "a", "b", "ab", "admin"], content.Elements.Select(static x => x.Id));
        Assert.Equal(["g"], content.Roots);
        Assert.Null(Clipboard.Copy(diagram.Snapshot, diagram.Index, ["ab"]));
    }

    [Fact]
    public void PasteGivesNewIdsAndDecidesTheParents()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("g", 0, 0, 400, 300));
        diagram.Add(Node("a", 20, 40, "g"));
        diagram.Add(Node("b", 200, 40, "g"));
        diagram.Connect(Link("ab", "a", "b"));
        var whole = Clipboard.Copy(diagram.Snapshot, diagram.Index, ["g"])!;
        var single = Clipboard.Copy(diagram.Snapshot, diagram.Index, ["a"])!;

        var (pasted, reason) = Clipboard.Paste(diagram.Snapshot, diagram.Index, whole, new DiagramPoint(20, 20));
        Assert.Null(reason);
        using (diagram.BeginUpdate())
        {
            foreach (var element in pasted!.Elements)
            {
                diagram.Add(element);
            }
        }

        var group = (DiagramGroup)diagram.Find(pasted.IdMap["g"])!;
        var node = (DiagramNode)diagram.Find(pasted.IdMap["a"])!;
        var link = (DiagramLink)diagram.Find(pasted.IdMap["ab"])!;
        Assert.Equal(new DiagramRect(20, 20, 400, 300), group.Bounds);
        Assert.Equal(group.Id, node.ParentId);
        Assert.Equal(new DiagramLinkEnd(pasted.IdMap["b"]), link.Target);
        Assert.Equal([group.Id], pasted.Selection);

        var (inPlace, _) = Clipboard.Paste(diagram.Snapshot, diagram.Index, single, new DiagramPoint(20, 20));
        Assert.Equal("g", ((DiagramBox)inPlace!.Elements[0]).ParentId);

        diagram.Delete(["g"]);
        var (topLevel, _) = Clipboard.Paste(diagram.Snapshot, diagram.Index, single, new DiagramPoint(20, 20));
        Assert.Null(((DiagramBox)topLevel!.Elements[0]).ParentId);
    }

    [Fact]
    public void PasteStopsAtTheMaximumCount()
    {
        var schema = Schema with { NodeTypes = [.. Schema.NodeTypes, new DiagramNodeType { Name = "only", MaxCount = 1 }] };
        var diagram = new Diagram(schema);
        diagram.Add(Node("a", 0, 0, type: "only"));

        var (result, reason) = Clipboard.Paste(diagram.Snapshot, diagram.Index, Clipboard.Copy(diagram.Snapshot, diagram.Index, ["a"])!, new DiagramPoint(20, 20));

        Assert.Null(result);
        Assert.Equal(DiagramReasonKind.Count, reason!.Kind);
    }

    [Fact]
    public void GroupWrapsTheSelectionWithThePadding()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 200, 0));

        var result = Grouping.Group(diagram.Snapshot, diagram.Index, ["a", "b"], "vpc");
        diagram.Write(null, DiagramChangeOrigin.User, result.Edit!);

        var group = (DiagramGroup)diagram.Find(result.Selection[0])!;
        Assert.Equal(new DiagramRect(-20, -40, 340, 110), group.Bounds);
        Assert.Equal(group.Id, ((DiagramBox)diagram.Find("a")!).ParentId);
        Assert.Equal(group.Id, ((DiagramBox)diagram.Find("b")!).ParentId);
    }

    [Fact]
    public void GroupNeedsOneParentAndAType()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("g", 0, 0, 400, 300));
        diagram.Add(Node("a", 20, 40, "g"));
        diagram.Add(Node("b", 500, 0));

        Assert.False(Grouping.CanGroup(diagram.Snapshot, diagram.Index, ["a", "b"], "vpc"));
        Assert.False(Grouping.CanGroup(diagram.Snapshot, diagram.Index, ["b"], null));
        Assert.True(Grouping.CanGroup(diagram.Snapshot, diagram.Index, ["b"], "vpc"));
        Assert.False(Grouping.CanGroup(diagram.Snapshot, diagram.Index, ["b"], "scaling"));
    }

    [Fact]
    public void UngroupMovesTheChildrenUp()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("outer", 0, 0, 800, 600));
        diagram.Add(Group("inner", 20, 40, 500, 400, "outer"));
        diagram.Add(Node("a", 40, 80, "inner"));
        diagram.Add(Node("b", 600, 80, "outer"));
        diagram.Connect(Link("toInner", "b", "inner"));

        var result = Grouping.Ungroup(diagram.Snapshot, diagram.Index, ["outer", "inner"]);
        diagram.Write(null, DiagramChangeOrigin.User, result.Edit!);

        Assert.Null(((DiagramBox)diagram.Find("a")!).ParentId);
        Assert.Null(((DiagramBox)diagram.Find("b")!).ParentId);
        Assert.Empty(diagram.Snapshot.Groups);
        Assert.Empty(diagram.Snapshot.Links);
        Assert.Equal(["a", "b"], result.Selection.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OverlayCommandsChangeTheMembers()
    {
        var schema = Schema with { GroupTypes = [.. Schema.GroupTypes, new DiagramGroupType { Name = "servers", Kind = DiagramGroupKind.Overlay, AllowedMembers = ["server"] }] };
        var diagram = new Diagram(schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 200, 0));
        diagram.Add(Node("c", 400, 0, type: "service"));

        var created = Grouping.CreateOverlay(diagram.Snapshot, diagram.Index, ["a"], "scaling");
        diagram.Write(null, DiagramChangeOrigin.User, created.Edit!);
        var id = created.Selection[0];
        diagram.Write(null, DiagramChangeOrigin.User, Grouping.AddMembers(diagram.Snapshot, diagram.Index, [id, "b"]).Edit!);
        Assert.Equal(["a", "b"], ((DiagramGroup)diagram.Find(id)!).MemberIds);

        diagram.Write(null, DiagramChangeOrigin.User, Grouping.RemoveMembers(diagram.Snapshot, [id, "a"]).Edit!);
        Assert.Equal(["b"], ((DiagramGroup)diagram.Find(id)!).MemberIds);

        Assert.Equal(DiagramReasonKind.Nesting, Grouping.CreateOverlay(diagram.Snapshot, diagram.Index, ["a", "c"], "servers").Reason!.Kind);
    }

    [Fact]
    public void AlignAndDistributeMoveOnlyMovableElements()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 100, 30));
        diagram.Add(Node("c", 400, 60));
        diagram.Add(Node("locked", 50, 90) with { Locked = true });

        Assert.Equal([("b", new DiagramPoint(-100, 0)), ("c", new DiagramPoint(-400, 0))], Arrangement.Align(diagram.Snapshot, diagram.Index, ["a", "b", "c", "locked"], AlignEdge.Left));
        Assert.Equal([("a", new DiagramPoint(0, 90)), ("b", new DiagramPoint(0, 60)), ("c", new DiagramPoint(0, 30))], Arrangement.Align(diagram.Snapshot, diagram.Index, ["a", "b", "c", "locked"], AlignEdge.Bottom));
        Assert.Equal([("b", new DiagramPoint(100, 0))], Arrangement.Distribute(diagram.Snapshot, diagram.Index, ["a", "b", "c"], true));
        Assert.False(Arrangement.CanAlign(diagram.Snapshot, diagram.Index, ["a"], 2));
    }

    private static string[] Ids<T>(IEnumerable<T> elements)
        where T : DiagramElement =>
        [.. elements.Select(static x => x.Id)];
}
