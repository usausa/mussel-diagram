namespace MusselDiagram;

using System.Text.Json;

using MusselDiagram.Internal;

using static MusselDiagram.TestDiagrams;

public sealed class JsonTests
{
    private static readonly DiagramSchema JsonSchema = Schema with
    {
        NodeTypes = [.. Schema.NodeTypes, new DiagramNodeType { Name = "typed", DataType = TestJsonContext.Default.ServerData }],
        SharedItemTypes = [new DiagramSharedItemType { Name = "config", DataType = TestJsonContext.Default.ServerData }]
    };

    [Fact]
    public void SaveWritesTheDocumentedFields()
    {
        using var document = JsonDocument.Parse(CreateDiagram().ToJson());
        var root = document.RootElement;

        Assert.Equal("mussel-diagram", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        var vpc = root.GetProperty("groups")[0];
        Assert.Equal("tree", vpc.GetProperty("kind").GetString());
        Assert.Equal(0, vpc.GetProperty("x").GetDouble());
        Assert.Equal(["select", "move"], vpc.GetProperty("permissions").EnumerateArray().Select(static x => x.GetString()));
        Assert.Equal(["cidr", "note"], vpc.GetProperty("texts").EnumerateObject().Select(static x => x.Name));
        Assert.False(vpc.TryGetProperty("parent", out _));
        Assert.False(vpc.TryGetProperty("locked", out _));
        var asg = root.GetProperty("groups")[1];
        Assert.Equal("overlay", asg.GetProperty("kind").GetString());
        Assert.False(asg.TryGetProperty("x", out _));
        Assert.Equal(["web"], asg.GetProperty("members").EnumerateArray().Select(static x => x.GetString()));
        var web = root.GetProperty("nodes")[0];
        Assert.True(web.GetProperty("locked").GetBoolean());
        Assert.Equal("vpc", web.GetProperty("parent").GetString());
        Assert.Equal("cfg", web.GetProperty("data").GetProperty("ConfigId").GetString());
        var link = root.GetProperty("links")[0];
        Assert.Equal("in", link.GetProperty("target").GetProperty("port").GetString());
        Assert.False(link.GetProperty("source").TryGetProperty("port", out _));
        Assert.Equal(2, link.GetProperty("route")[0].GetArrayLength());
        Assert.Equal("Config", root.GetProperty("sharedItems")[0].GetProperty("label").GetString());
    }

    [Fact]
    public void LoadReadsWhatSaveWrote()
    {
        var diagram = CreateDiagram();
        var json = diagram.ToJson();
        var loaded = new Diagram(JsonSchema);

        var issues = loaded.LoadJson(json);

        Assert.Empty(issues);
        Assert.True(SnapshotContent.Same(diagram.Snapshot, loaded.Snapshot));
        Assert.IsType<ServerData>(loaded.Find("web")!.Data);
        Assert.Equal(json, loaded.ToJson());
    }

    [Fact]
    public void LoadFixesBrokenStructures()
    {
        var diagram = new Diagram(JsonSchema);

        var issues = diagram.LoadJson(
            """
            {
              "format": "mussel-diagram",
              "version": 1,
              "groups": [
                { "id": "g1", "type": "vpc", "kind": "tree", "x": 0, "y": 0, "width": 400, "height": 300, "parent": "g2" },
                { "id": "g2", "type": "vpc", "kind": "tree", "x": 0, "y": 0, "width": 400, "height": 300, "parent": "g1" },
                { "id": "kind", "type": "scaling", "kind": "tree", "x": 500, "y": 0, "width": 100, "height": 100 },
                { "id": "asg", "type": "scaling", "kind": "overlay", "members": ["n1", "missing", "l1"] },
                { "id": "empty", "type": "scaling", "kind": "overlay", "members": ["missing"] }
              ],
              "nodes": [
                { "id": "n1", "type": "server", "x": 40, "y": 80, "width": 100, "height": 50, "parent": "nowhere" },
                { "id": "n1", "type": "server", "x": 0, "y": 0, "width": 100, "height": 50 },
                { "id": "n2", "type": "server", "x": 300, "y": 0, "width": 0, "height": 50 },
                { "id": "n3", "type": "typed", "x": 600, "y": 0, "width": 100, "height": 50, "data": { "Name": 5 } }
              ],
              "links": [
                { "id": "l1", "type": "arrow", "source": { "element": "n1" }, "target": { "element": "n2" } },
                { "id": "l2", "type": "arrow", "source": { "element": "n1" }, "target": { "element": "gone" } },
                { "id": "l3", "type": "arrow", "source": { "element": "n1" } }
              ],
              "extra": true
            }
            """);

        Assert.Equal(
            [
                (DiagramIssueKind.KindMismatch, "kind"),
                (DiagramIssueKind.DuplicateId, "n1"),
                (DiagramIssueKind.InvalidSize, "n2"),
                (DiagramIssueKind.UnreadableData, "n3"),
                (DiagramIssueKind.MissingEnd, "l3"),
                (DiagramIssueKind.InvalidParent, "n1"),
                (DiagramIssueKind.InvalidParent, "g1"),
                (DiagramIssueKind.InvalidMember, "asg"),
                (DiagramIssueKind.InvalidMember, "asg"),
                (DiagramIssueKind.InvalidMember, "empty"),
                (DiagramIssueKind.MissingEnd, "l2")
            ],
            issues.Select(static x => (x.Kind, x.ElementIds[0])));
        var snapshot = diagram.Snapshot;
        Assert.Equal(["g1", "g2", "kind", "asg"], snapshot.Groups.Select(static x => x.Id));
        Assert.Null(snapshot.GetParent("g1"));
        Assert.Equal("g1", snapshot.GetParent("g2")!.Id);
        Assert.Null(snapshot.GetParent("n1"));
        Assert.Equal(new DiagramRect(300, 0, 120, 60), ((DiagramBox)snapshot.Find("n2")!).Bounds);
        Assert.IsType<JsonElement>(snapshot.Find("n3")!.Data);
        Assert.Equal(["l1"], snapshot.Links.Select(static x => x.Id));
        Assert.Equal(["n1"], ((DiagramGroup)snapshot.Find("asg")!).MemberIds);
        Assert.Contains(diagram.Validate(), static x => (x.Kind == DiagramIssueKind.UnregisteredType) && (x.ElementIds[0] == "kind"));
    }

    [Fact]
    public void LoadRejectsWhatIsNotADiagram()
    {
        var diagram = new Diagram(JsonSchema);
        diagram.Add(Node("a", 0, 0));
        var before = diagram.Snapshot;

        Assert.Throws<DiagramException>(() => diagram.LoadJson("not json"));
        Assert.Throws<DiagramException>(() => diagram.LoadJson("null"));
        Assert.Throws<DiagramException>(() => diagram.LoadJson("""{ "format": "other", "version": 1 }"""));
        Assert.Throws<DiagramException>(() => diagram.LoadJson("""{ "format": "mussel-diagram", "version": 2 }"""));
        Assert.Throws<DiagramException>(() => diagram.LoadJson("""{ "format": "mussel-diagram" }"""));
        Assert.Same(before, diagram.Snapshot);
    }

    [Fact]
    public void RoutesAreKeptOnlyWhenTheEndsStillMatch()
    {
        var diagram = new Diagram(JsonSchema);

        diagram.LoadJson(
            """
            {
              "format": "mussel-diagram",
              "version": 1,
              "nodes": [
                { "id": "a", "type": "server", "x": 0, "y": 0, "width": 100, "height": 50 },
                { "id": "b", "type": "server", "x": 300, "y": 0, "width": 100, "height": 50 }
              ],
              "links": [
                { "id": "keep", "type": "arrow", "source": { "element": "a" }, "target": { "element": "b" }, "route": [[100, 25], [200, 100], [300, 25]] },
                { "id": "fix", "type": "arrow", "source": { "element": "a" }, "target": { "element": "b" }, "route": [[0, 0], [300, 25]] },
                { "id": "none", "type": "arrow", "source": { "element": "a" }, "target": { "element": "b" } }
              ]
            }
            """);

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(200, 100), new DiagramPoint(300, 25)], Route("keep"));
        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(300, 25)], Route("fix"));
        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(300, 25)], Route("none"));
        Assert.All(diagram.Snapshot.Links, static x => Assert.Equal(DiagramRouteState.Routed, x.RouteState));

        ImmutableArray<DiagramPoint> Route(string id) => ((DiagramLink)diagram.Find(id)!).Route;
    }

    [Fact]
    public void LoadClearsTheHistoryAndTheModifiedFlag()
    {
        var diagram = new Diagram(Schema);
        Assert.False(diagram.IsModified);
        diagram.Add(Node("a", 0, 0));
        Assert.True(diagram.IsModified);
        var json = diagram.ToJson();
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.LoadJson(json);

        Assert.True(change!.IsReset);
        Assert.Equal(DiagramChangeOrigin.Load, change.Origin);
        Assert.Empty(change.Added);
        Assert.Equal(2, diagram.Snapshot.Version);
        Assert.False(diagram.CanUndo);
        Assert.False(diagram.IsModified);

        diagram.Move(["a"], new DiagramPoint(10, 0));
        Assert.True(diagram.IsModified);
        diagram.Move(["a"], new DiagramPoint(-10, 0));
        Assert.False(diagram.IsModified);
        diagram.Undo();
        Assert.True(diagram.IsModified);
        diagram.Undo();
        Assert.False(diagram.IsModified);
        diagram.Add(Node("b", 0, 0), new DiagramWriteOptions { History = DiagramHistoryMode.Skip });
        Assert.True(diagram.IsModified);
        diagram.MarkSaved();
        Assert.False(diagram.IsModified);
    }

    [Fact]
    public void StreamsWorkWithAndWithoutADiagram()
    {
        var source = CreateDiagram();
        using var stream = new MemoryStream();

        DiagramJson.Write(stream, source.Snapshot, JsonSchema);
        stream.Position = 0;
        var result = DiagramJson.Read(stream, JsonSchema);
        Assert.Empty(result.Issues);
        Assert.True(SnapshotContent.Same(source.Snapshot, result.Snapshot));

        stream.SetLength(0);
        source.Save(stream);
        stream.Position = 0;
        var target = new Diagram(JsonSchema);
        Assert.Empty(target.Load(stream));
        Assert.True(SnapshotContent.Same(source.Snapshot, target.Snapshot));
    }

    [Fact]
    public async Task StreamsWorkAsynchronously()
    {
        var source = CreateDiagram();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();

        await DiagramJson.WriteAsync(stream, source.Snapshot, JsonSchema, cancellationToken);
        stream.Position = 0;
        var result = await DiagramJson.ReadAsync(stream, JsonSchema, cancellationToken);
        Assert.True(SnapshotContent.Same(source.Snapshot, result.Snapshot));

        stream.SetLength(0);
        await source.SaveAsync(stream, cancellationToken);
        stream.Position = 0;
        var target = new Diagram(JsonSchema);
        Assert.Empty(await target.LoadAsync(stream, cancellationToken: cancellationToken));
        Assert.True(SnapshotContent.Same(source.Snapshot, target.Snapshot));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(JsonSchema);
        diagram.Add(Group("vpc", 0, 0, 600, 400) with
        {
            Label = "VPC",
            Texts = ImmutableDictionary<string, string>.Empty.Add("note", "n").Add("cidr", "10.0.0.0/16"),
            Icon = "aws:vpc",
            Permissions = DiagramPermissions.Select | DiagramPermissions.Move
        });
        diagram.Add(Node("web", 40, 80, "vpc", type: "typed") with { Data = new ServerData("web", "cfg"), Locked = true });
        diagram.Add(Node("db", 300, 80, "vpc", type: "service"));
        diagram.Add(new DiagramGroup { Id = "asg", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["web"] });
        diagram.Connect(Link("l1", "web", "db", targetPort: "in"));
        diagram.Add(new DiagramSharedItem { Id = "cfg", Type = "config", Label = "Config", Data = new ServerData("cfg") });
        return diagram;
    }
}
