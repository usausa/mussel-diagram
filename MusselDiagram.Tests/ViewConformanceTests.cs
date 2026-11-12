namespace MusselDiagram;

using System.Text.Json;

public sealed class ViewConformanceTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "server" },
            new DiagramNodeType { Name = "start", Look = new DiagramLook { Shape = DiagramShape.Ellipse, Fill = "#fde68a" } },
            new DiagramNodeType
            {
                Name = "process",
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "data" },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }
                ]
            }
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "vpc", FillHitTest = true },
            new DiagramGroupType { Name = "team", Kind = DiagramGroupKind.Overlay }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        SharedItemTypes = [new DiagramSharedItemType { Name = "config" }],
        DefaultLinkType = "arrow"
    };

    private readonly BunitJSModuleInterop module;

    public ViewConformanceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public async Task ReconnectingTellsBothEndsAndTheLinkType()
    {
        var diagram = CreateDiagram();
        var reconnecting = new List<DiagramReconnectingEventArgs>();
        var reconnected = new List<DiagramReconnectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Reconnecting, reconnecting.Add)
            .Add(p => p.Reconnected, reconnected.Add));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitReconnectAsync("l", DiagramLinkEndSide.Target, new DiagramLinkEnd("p3", "in"), diagram.Snapshot.Version, 0));

        Assert.True(result.Accepted);
        var before = Assert.Single(reconnecting);
        var after = Assert.Single(reconnected);
        Assert.Equal((new DiagramLinkEnd("p1", "out"), new DiagramLinkEnd("p3", "in"), "arrow"), (before.Source, before.Target, before.LinkType));
        Assert.Equal((new DiagramLinkEnd("p1", "out"), new DiagramLinkEnd("p3", "in"), "arrow"), (after.Source, after.Target, after.LinkType));
    }

    [Fact]
    public async Task PlacingFromALinkAnswersWithTheNewElement()
    {
        var diagram = CreateDiagram();
        var connected = new List<DiagramConnectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Connected, connected.Add));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitPlaceAsync("process", new DiagramRect(900, 400, 120, 60), null, new DiagramLinkEnd("p3", "out"), "arrow", diagram.Snapshot.Version, 0));

        Assert.True(result.Accepted);
        Assert.Equal(Assert.Single(connected).PlacedElement!.Id, result.ElementId);
    }

    [Fact]
    public async Task SelectedGroupsKeepWhatMovesWithThemDrawn()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, 0, 0, 400, 300));

        Assert.Empty(cut.FindAll("g[data-id='far']"));
        Assert.Empty(cut.FindAll("g[data-id='m']"));

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["vpc", "o"]).AsTask());

        Assert.Single(cut.FindAll("g[data-id='far']"));
        Assert.Single(cut.FindAll("g[data-id='m']"));
        Assert.Single(cut.FindAll("g[data-id='o']"));
    }

    [Fact]
    public void PortsAndGroupsCarryWhatStylesNeed()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        var input = cut.Find("g[data-id='p1'] circle[data-port='in']");
        var output = cut.Find("g[data-id='p1'] circle[data-port='out']");

        Assert.Equal(("in", "data"), (input.GetAttribute("data-direction"), input.GetAttribute("data-kind")));
        Assert.Equal("out", output.GetAttribute("data-direction"));
        Assert.False(output.HasAttribute("data-kind"));
        Assert.Contains("mussel-diagram-fill-hit", cut.Find("g[data-id='vpc']").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedItemsAreNotSelected()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["settings", "p1"]).AsTask());
        Assert.Equal(["p1"], cut.Instance.SelectedIds);

        await cut.InvokeAsync(() => cut.Instance.OnSelectionChangedAsync(["p2", "settings"]));
        Assert.Equal(["p2"], cut.Instance.SelectedIds);
    }

    [Fact]
    public async Task GroupsOfUnregisteredTypesTakeNoChildren()
    {
        var diagram = new Diagram(Schema);
        diagram.LoadJson("""
            {
              "format": "mussel-diagram",
              "version": 1,
              "groups": [{ "id": "x", "type": "gone", "x": 0, "y": 0, "width": 400, "height": 300 }],
              "nodes": [{ "id": "a", "type": "server", "x": 500, "y": 0, "width": 80, "height": 40 }]
            }
            """);
        var rejected = new List<DiagramCommitRejectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.CommitRejected, rejected.Add));

        var moved = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(-400, 100), true, "x", null, diagram.Snapshot.Version, 0));
        var dropped = await cut.InvokeAsync(() => cut.Instance.CommitDropAsync("server", new DiagramRect(20, 20, 80, 40), "x", null, null, diagram.Snapshot.Version, moved.Rejections));

        Assert.False(moved.Accepted);
        Assert.False(dropped.Accepted);
        Assert.All(rejected, static x => Assert.Equal(DiagramReasonKind.Nesting, x.Reason.Kind));
        Assert.Null(((DiagramBox)diagram.Find("a")!).ParentId);
    }

    [Fact]
    public async Task UndoAndRedoSelectWhatTheStepChanged()
    {
        var diagram = CreateDiagram();
        diagram.ClearHistory();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["p1"], new DiagramPoint(10, 0), false, null, null, diagram.Snapshot.Version, 0));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["p3"]).AsTask());

        await cut.InvokeAsync(() => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Undo));
        Assert.Equal(["p1"], cut.Instance.SelectedIds);

        await cut.InvokeAsync(() => cut.Instance.ClearSelectionAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Redo));
        Assert.Equal(["p1"], cut.Instance.SelectedIds);

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["p2"]).AsTask());
        await cut.InvokeAsync(() => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Delete));
        await cut.InvokeAsync(() => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Undo));
        Assert.Equal(["p2"], cut.Instance.SelectedIds);
        Assert.True(diagram.Snapshot.Contains("l"));
    }

    [Fact]
    public async Task ViewsOfOneDiagramCommitOneAtATime()
    {
        var diagram = CreateDiagram();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var first = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moving, _ => release.Task)
            .Add(p => p.Moved, _ => order.Add("first")));
        var second = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moved, _ => order.Add("second")));
        var version = diagram.Snapshot.Version;

        var waiting = first.InvokeAsync(() => first.Instance.CommitMoveAsync(["p1"], new DiagramPoint(10, 0), false, null, null, version, 0));
        var queued = second.InvokeAsync(() => second.Instance.CommitMoveAsync(["p3"], new DiagramPoint(10, 0), false, null, null, version, 0));

        Assert.False(queued.IsCompleted);
        release.SetResult();

        Assert.True((await waiting.ConfigureAwait(true)).Accepted);
        Assert.True((await queued.ConfigureAwait(true)).Accepted);
        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public async Task CommitsActOnTheSelectionTheUserSaw()
    {
        var diagram = CreateDiagram();
        IReadOnlyList<string>? seen = null;
        IRenderedComponent<MusselDiagramView>? cut = null;
        cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moving, _ => seen = cut!.Instance.SelectedIds));

        await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["p1"], new DiagramPoint(10, 0), false, null, null, diagram.Snapshot.Version, 0, ["p1", "p2", "settings"]));

        Assert.Equal(["p1", "p2"], seen);
        Assert.Equal(["p1", "p2"], cut.Instance.SelectedIds);
    }

    [Fact]
    public void TheHitWidthOfLinksGoesToTheScript()
    {
        Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(static p => p.LinkHitWidth, 16));

        var options = (JsonElement)module.Invocations["attach"][0].Arguments[2]!;
        Assert.Equal(16, options.GetProperty("linkHitWidth").GetDouble());
    }

    [Fact]
    public void ThePaletteDrawsTheShapeOfEachType()
    {
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

        var start = palette.Find(".mussel-diagram-palette-item[data-mussel-type='start'] .mussel-diagram-palette-shape");
        var server = palette.Find(".mussel-diagram-palette-item[data-mussel-type='server'] .mussel-diagram-palette-shape");

        Assert.Equal(("ellipse", "--mussel-diagram-shape-fill: #fde68a;"), (start.LocalName, start.GetAttribute("style")));
        Assert.Equal("rect", server.LocalName);
        Assert.False(server.HasAttribute("style"));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 4000, 300) });
        diagram.Add(new DiagramNode { Id = "far", Type = "server", Bounds = new DiagramRect(3500, 100, 80, 40), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "m", Type = "server", Bounds = new DiagramRect(5000, 0, 80, 40) });
        diagram.Add(new DiagramGroup { Id = "o", Type = "team", Kind = DiagramGroupKind.Overlay, MemberIds = ["m"] });
        diagram.Add(new DiagramNode { Id = "p1", Type = "process", Bounds = new DiagramRect(0, 400, 120, 60) });
        diagram.Add(new DiagramNode { Id = "p2", Type = "process", Bounds = new DiagramRect(300, 400, 120, 60) });
        diagram.Add(new DiagramNode { Id = "p3", Type = "process", Bounds = new DiagramRect(600, 400, 120, 60) });
        diagram.Add(new DiagramSharedItem { Id = "settings", Type = "config" });
        diagram.Connect(new DiagramLink { Id = "l", Type = "arrow", Source = new DiagramLinkEnd("p1", "out"), Target = new DiagramLinkEnd("p2", "in") });
        return diagram;
    }
}
