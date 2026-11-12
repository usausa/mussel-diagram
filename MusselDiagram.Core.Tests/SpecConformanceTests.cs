namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed class SpecConformanceTests
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "step", Start = true, Ports = [new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }] },
            new DiagramNodeType
            {
                Name = "task",
                RowHeaderHeight = 30,
                RowHeight = 24,
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Row = 0 },
                    new DiagramPortDefinition { Id = "yes", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Row = 1 },
                    new DiagramPortDefinition { Id = "top", Side = DiagramSide.Top, Row = 3 }
                ]
            },
            new DiagramNodeType { Name = "pinned", StayInParent = true }
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "box", Padding = new DiagramThickness(10, 10, 10, 10) },
            new DiagramGroupType { Name = "single", MaxCount = 1 },
            new DiagramGroupType { Name = "fixed", Undeletable = true, Kind = DiagramGroupKind.Overlay }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        DefaultLinkType = "arrow"
    };

    [Fact]
    public void LinksToPortsThatNoLongerExistAreKeptWhenLoading()
    {
        var json = """
            {
              "format": "mussel-diagram",
              "version": 1,
              "nodes": [
                { "id": "a", "type": "step", "x": 0, "y": 0, "width": 80, "height": 40 },
                { "id": "b", "type": "task", "x": 200, "y": 0, "width": 120, "height": 100 }
              ],
              "links": [{ "id": "ab", "type": "arrow", "source": { "element": "a", "port": "gone" }, "target": { "element": "b", "port": "in" } }]
            }
            """;
        var diagram = new Diagram(Schema);

        diagram.LoadJson(json);

        Assert.Equal("gone", diagram.Snapshot.Links.Single().Source.PortId);
        Assert.Contains(diagram.Validate(), static x => x.Kind == DiagramIssueKind.MissingPort);
    }

    [Fact]
    public void MovesMustBeFinite()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "a", Type = "step", Bounds = new DiagramRect(0, 0, 80, 40) });

        Assert.Throws<DiagramException>(() => diagram.Move(["a"], new DiagramPoint(double.NaN, 0)));
        Assert.Equal(new DiagramRect(0, 0, 80, 40), ((DiagramBox)diagram.Find("a")!).Bounds);
    }

    [Fact]
    public void UpdatesThatEndWhereTheyStartedRecordNothing()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "a", Type = "step", Bounds = new DiagramRect(0, 0, 80, 40) });
        diagram.ClearHistory();
        var version = diagram.Snapshot.Version;
        var changes = 0;
        diagram.Changed += (_, _) => changes++;

        using (diagram.BeginUpdate())
        {
            diagram.Move(["a"], new DiagramPoint(10, 0));
            diagram.Move(["a"], new DiagramPoint(-10, 0));
        }

        Assert.Equal(0, changes);
        Assert.Equal(version, diagram.Snapshot.Version);
        Assert.False(diagram.CanUndo);
    }

    [Fact]
    public void GroupingKeepsElementsThatStayInTheirParentAndTheCountLimit()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "g", Type = "box", Bounds = new DiagramRect(0, 0, 400, 300) });
        diagram.Add(new DiagramNode { Id = "p", Type = "pinned", Bounds = new DiagramRect(20, 20, 80, 40), ParentId = "g" });
        diagram.Add(new DiagramNode { Id = "a", Type = "step", Bounds = new DiagramRect(500, 0, 80, 40) });
        diagram.Add(new DiagramGroup { Id = "s", Type = "single", Bounds = new DiagramRect(700, 0, 200, 200) });

        Assert.Equal(DiagramReasonKind.Nesting, Grouping.Group(diagram.Snapshot, diagram.Index, ["p"], "box").Reason?.Kind);
        Assert.Equal(DiagramReasonKind.Nesting, Grouping.Ungroup(diagram.Snapshot, diagram.Index, ["g"]).Reason?.Kind);
        Assert.Equal(DiagramReasonKind.Count, Grouping.Group(diagram.Snapshot, diagram.Index, ["a"], "single").Reason?.Kind);
    }

    [Fact]
    public void DeletingGroupsByUngroupingAsksTheAppAndKeepsUndeletableGroups()
    {
        var schema = Schema with { Rules = new DiagramAppRules { CanDelete = static x => x.ElementIds.Contains("g") ? DiagramRuleResult.Reject("Keep it.") : DiagramRuleResult.Allow } };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramGroup { Id = "g", Type = "box", Bounds = new DiagramRect(0, 0, 400, 300) });
        diagram.Add(new DiagramNode { Id = "a", Type = "step", Bounds = new DiagramRect(20, 20, 80, 40), ParentId = "g" });
        diagram.Add(new DiagramGroup { Id = "o", Type = "fixed", Kind = DiagramGroupKind.Overlay, MemberIds = ["a"] });

        var ungroup = Grouping.Ungroup(diagram.Snapshot, diagram.Index, ["g"]);
        var remove = Grouping.RemoveMembers(diagram.Snapshot, diagram.Index, ["o", "a"]);

        Assert.Equal(("Keep it.", DiagramReasonKind.App), (ungroup.Reason?.Message, ungroup.Reason!.Kind));
        Assert.Equal(DiagramReasonKind.Permission, remove.Reason?.Kind);
    }

    [Fact]
    public void AligningKeepsElementsInsideTheirParent()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "g", Type = "box", Bounds = new DiagramRect(0, 0, 300, 200) });
        diagram.Add(new DiagramNode { Id = "p", Type = "pinned", Bounds = new DiagramRect(20, 20, 80, 40), ParentId = "g" });
        diagram.Add(new DiagramNode { Id = "far", Type = "step", Bounds = new DiagramRect(600, 20, 80, 40) });

        var moves = Arrangement.Align(diagram.Snapshot, diagram.Index, ["p", "far"], AlignEdge.Right);

        var (_, offset) = moves.Single(static x => x.Id == "p");
        Assert.Equal(new DiagramPoint(190, 0), offset);
        Assert.True(StayRules.Allows(diagram.Snapshot, diagram.Index, ["p"], new DiagramPoint(190, 0)));
        Assert.False(StayRules.Allows(diagram.Snapshot, diagram.Index, ["p"], new DiagramPoint(191, 0)));
    }

    [Fact]
    public void PortRowsAreMeasuredFromTheTopOfTheElement()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "t", Type = "task", Bounds = new DiagramRect(100, 50, 120, 200) });

        var ports = diagram.GetPorts("t").ToDictionary(static x => x.Id);

        Assert.Equal(new DiagramPoint(100, 92), ports["in"].Position);
        Assert.Equal(new DiagramPoint(220, 116), ports["yes"].Position);
        Assert.Equal(new DiagramPoint(160, 50), ports["top"].Position);
    }

    [Fact]
    public void UnreachableElementsStartFromTheStartTypes()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "s", Type = "step", Bounds = new DiagramRect(0, 0, 80, 40) });
        diagram.Add(new DiagramNode { Id = "t", Type = "task", Bounds = new DiagramRect(200, 0, 120, 100) });
        diagram.Add(new DiagramNode { Id = "lost", Type = "task", Bounds = new DiagramRect(400, 0, 120, 100) });
        diagram.Connect(new DiagramLink { Id = "st", Type = "arrow", Source = new DiagramLinkEnd("s", "out"), Target = new DiagramLinkEnd("t", "in") });

        Assert.Equal(["lost"], diagram.Snapshot.GetUnreachable(Schema).Select(static x => x.Id));
    }

    [Fact]
    public void IconsWrittenInCodeAreSanitized()
    {
        var set = new DiagramIconSet { Prefix = "app", Icons = [new DiagramIcon { Id = "bad", Body = "<script>alert(1)</script><rect id=\"r\" width=\"4\" height=\"4\" onclick=\"x()\" />" }] };

        var body = SvgSanitizer.Body(set, set.Icons[0]);

        Assert.DoesNotContain("script", body, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", body, StringComparison.Ordinal);
        Assert.Contains("id=\"app-bad-r\"", body, StringComparison.Ordinal);
    }
}
