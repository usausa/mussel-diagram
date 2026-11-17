namespace MusselDiagram;

public sealed class OperationCommitTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "server" },
            new DiagramNodeType { Name = "pinned", StayInParent = true },
            new DiagramNodeType { Name = "small", MinSize = new DiagramSize(40, 40) },
            new DiagramNodeType
            {
                Name = "process",
                MaxCount = 3,
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, MaxLinks = 1 },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }
                ]
            }
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "vpc" },
            new DiagramGroupType { Name = "servers", AllowedChildren = ["server"] },
            new DiagramGroupType { Name = "fit", Sizing = DiagramGroupSizing.Content }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        DefaultLinkType = "arrow",
        Rules = new DiagramAppRules { CanConnect = static x => x.Target.ElementId == "blocked" ? DiagramRuleResult.Reject("Not this one") : DiagramRuleResult.Allow }
    };

    public OperationCommitTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public async Task MovesCanChangeTheParent()
    {
        var diagram = CreateDiagram();
        var moving = new List<DiagramMovingEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moving, moving.Add));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(400, 0), true, "servers", null, cut.Instance.ScriptVersion, 0));
        var nested = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["p1"], new DiagramPoint(0, 10), true, "servers", null, cut.Instance.ScriptVersion, accepted.Rejections));
        var pinned = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["pin"], new DiagramPoint(0, 10), true, null, null, cut.Instance.ScriptVersion, nested.Rejections));

        Assert.True(accepted.Accepted);
        Assert.Equal("servers", ((DiagramBox)diagram.Find("a")!).ParentId);
        Assert.Equal(accepted.Version, diagram.Snapshot.Version);
        Assert.True(moving[0].ParentChanged);
        Assert.Equal("servers", moving[0].NewParentId);
        Assert.False(nested.Accepted);
        Assert.False(pinned.Accepted);
        diagram.Undo();
        Assert.Equal("vpc", ((DiagramBox)diagram.Find("a")!).ParentId);
    }

    [Fact]
    public async Task ResizesFollowThePermissionsAndTheMinimum()
    {
        var diagram = CreateDiagram();
        var resized = new List<DiagramResizedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Resized, resized.Add));
        var origin = cut.Instance.ScriptVersion;

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitResizeAsync("a", new DiagramRect(40, 80, 150, 80), origin, 0));
        var written = ((DiagramBox)diagram.Find("a")!).Bounds;
        await cut.InvokeAsync(() => diagram.Move(["a"], new DiagramPoint(0, 5)));
        var stale = await cut.InvokeAsync(() => cut.Instance.CommitResizeAsync("a", new DiagramRect(40, 80, 160, 80), origin, accepted.Rejections));
        var tooSmall = await cut.InvokeAsync(() => cut.Instance.CommitResizeAsync("tiny", new DiagramRect(0, 600, 20, 20), cut.Instance.ScriptVersion, stale.Rejections));
        var content = await cut.InvokeAsync(() => cut.Instance.CommitResizeAsync("fit", new DiagramRect(0, 0, 10, 10), cut.Instance.ScriptVersion, tooSmall.Rejections));

        Assert.True(accepted.Accepted);
        Assert.Equal(new DiagramRect(40, 80, 150, 80), written);
        Assert.Equal("a", Assert.Single(resized).ElementId);
        Assert.False(stale.Accepted);
        Assert.False(tooSmall.Accepted);
        Assert.False(content.Accepted);
    }

    [Fact]
    public async Task AHandlerCanAwaitACommand()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        cut.Render(parameters => parameters.Add(p => p.Moved, _ => cut.Instance.Commands.ExecuteAsync(DiagramCommands.ToggleLock)));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        var commit = cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));
        var result = await commit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.Accepted);
        Assert.True(((DiagramBox)diagram.Find("a")!).Locked);
    }

    [Fact]
    public async Task ABeforeHandlerCanAwaitACommand()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        cut.Render(parameters => parameters.Add(p => p.Moving, _ => cut.Instance.Commands.ExecuteAsync(DiagramCommands.ToggleLock)));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        var commit = cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));
        var result = await commit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(result.Accepted);
        var box = (DiagramBox)diagram.Find("a")!;
        Assert.True(box.Locked);
        Assert.Equal(40, box.Bounds.X);
    }

    [Fact]
    public async Task ARejectionHandlerCanAwaitACommand()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        cut.Render(parameters => parameters.Add(p => p.CommitRejected, _ => cut.Instance.Commands.ExecuteAsync(DiagramCommands.ToggleLock)));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        var commit = cut.InvokeAsync(() => cut.Instance.CommitConnectAsync(new DiagramLinkEnd("a"), new DiagramLinkEnd("blocked"), "arrow", 0));
        var result = await commit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(result.Accepted);
        Assert.True(((DiagramBox)diagram.Find("a")!).Locked);
    }

    [Fact]
    public async Task AHandlerOfACommandCanAwaitACommand()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));
        cut.Render(parameters => parameters.Add(p => p.Deleting, _ => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Undo)));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["p1"]).AsTask());

        var delete = cut.InvokeAsync(() => cut.Instance.Commands.ExecuteAsync(DiagramCommands.Delete));
        await delete.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(diagram.Snapshot.Contains("p1"));
        Assert.Equal(40, ((DiagramBox)diagram.Find("a")!).Bounds.X);
    }

    [Fact]
    public async Task ACommandThatAHandlerDoesNotAwaitRunsAtOnce()
    {
        var diagram = CreateDiagram();
        var lockedInHandler = false;
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        cut.Render(parameters => parameters.Add(p => p.Moved, args =>
        {
            _ = cut.Instance.Commands.ExecuteAsync(DiagramCommands.ToggleLock);
            lockedInHandler = ((DiagramBox)diagram.Find("a")!).Locked;
        }));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        var commit = cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));
        var result = await commit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.Accepted);
        Assert.True(lockedInHandler);
    }

    [Fact]
    public async Task CallsFromTheScriptAfterDisposingDoNothing()
    {
        var diagram = CreateDiagram();
        DiagramViewport? reported = null;
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram).Add(p => p.ViewportChanged, x => reported = x));
        var bridge = new MusselDiagram.Internal.DiagramScriptBridge(cut.Instance);
        var version = diagram.Snapshot.Version;
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.CommitMoveAsync(["a"], 10, 0, false, null, null, default, 0, [])));
        await cut.InvokeAsync(() => bridge.OnViewportChanged(2, 0, 0, 100, 100));

        Assert.Null(reported);
        Assert.Equal(version, diagram.Snapshot.Version);
    }

    [Fact]
    public async Task ConnectionsPassTheRulesAndTheEvents()
    {
        var diagram = CreateDiagram();
        var connected = new List<DiagramConnectedEventArgs>();
        var rejected = new List<DiagramCommitRejectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Connected, connected.Add)
            .Add(p => p.CommitRejected, rejected.Add));

        var accepted = await cut.InvokeAsync(() => cut.Instance.CommitConnectAsync(new DiagramLinkEnd("p1", "out"), new DiagramLinkEnd("p2", "in"), "arrow", 0));
        var full = await cut.InvokeAsync(() => cut.Instance.CommitConnectAsync(new DiagramLinkEnd("a"), new DiagramLinkEnd("p2", "in"), "arrow", accepted.Rejections));
        var app = await cut.InvokeAsync(() => cut.Instance.CommitConnectAsync(new DiagramLinkEnd("a"), new DiagramLinkEnd("blocked"), "arrow", full.Rejections));

        Assert.True(accepted.Accepted);
        var link = (DiagramLink)diagram.Find(Assert.Single(connected).LinkId)!;
        Assert.Equal(new DiagramLinkEnd("p2", "in"), link.Target);
        Assert.False(full.Accepted);
        Assert.Equal(DiagramReasonKind.Limit, rejected[0].Reason.Kind);
        Assert.False(app.Accepted);
        Assert.Equal("Not this one", app.Reason);
        Assert.Equal(DiagramOperation.Connect, rejected[1].Operation);
    }

    [Fact]
    public async Task ReconnectingIgnoresTheMovedLinkForLimits()
    {
        var diagram = CreateDiagram();
        diagram.Connect(new DiagramLink { Id = "l", Type = "arrow", Source = new DiagramLinkEnd("p1", "out"), Target = new DiagramLinkEnd("p2", "in") });
        var reconnected = new List<DiagramReconnectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Reconnected, reconnected.Add));

        var same = await cut.InvokeAsync(() => cut.Instance.CommitReconnectAsync("l", DiagramLinkEndSide.Source, new DiagramLinkEnd("a"), cut.Instance.ScriptVersion, 0));
        var other = await cut.InvokeAsync(() => cut.Instance.CommitReconnectAsync("l", DiagramLinkEndSide.Target, new DiagramLinkEnd("blocked"), cut.Instance.ScriptVersion, same.Rejections));

        Assert.True(same.Accepted);
        Assert.Equal(new DiagramLinkEnd("a"), ((DiagramLink)diagram.Find("l")!).Source);
        Assert.Equal(DiagramLinkEndSide.Source, Assert.Single(reconnected).End);
        Assert.False(other.Accepted);
    }

    [Fact]
    public async Task PlacingConnectsAtTheFirstPointThatFits()
    {
        var diagram = CreateDiagram();
        var connected = new List<DiagramConnectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Connected, connected.Add));
        var version = diagram.Snapshot.Version;

        var placed = await cut.InvokeAsync(() => cut.Instance.CommitPlaceAsync("process", new DiagramRect(900, 0, 120, 60), null, new DiagramLinkEnd("a"), "arrow", 0));
        var full = await cut.InvokeAsync(() => cut.Instance.CommitPlaceAsync("process", new DiagramRect(900, 200, 120, 60), null, new DiagramLinkEnd("a"), "arrow", placed.Rejections));

        Assert.True(placed.Accepted);
        var e = Assert.Single(connected);
        Assert.Equal("process", e.PlacedElement!.Type);
        Assert.Equal(new DiagramLinkEnd(e.PlacedElement.Id, "in"), e.Target);
        Assert.Equal(version + 1, diagram.Snapshot.Version);
        Assert.False(full.Accepted);
        Assert.Equal("The diagram has the maximum number of this type.", full.Reason);
        diagram.Undo();
        Assert.False(diagram.Snapshot.Contains(e.PlacedElement.Id));
        Assert.False(diagram.Snapshot.Contains(e.LinkId));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 400, 300) });
        diagram.Add(new DiagramGroup { Id = "servers", Type = "servers", Bounds = new DiagramRect(420, 0, 400, 300) });
        diagram.Add(new DiagramGroup { Id = "fit", Type = "fit", Bounds = new DiagramRect(0, 1000, 100, 100) });
        diagram.Add(new DiagramNode { Id = "a", Type = "server", Bounds = new DiagramRect(40, 80, 100, 50), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "pin", Type = "pinned", Bounds = new DiagramRect(200, 80, 100, 50), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "p1", Type = "process", Bounds = new DiagramRect(0, 400, 120, 60) });
        diagram.Add(new DiagramNode { Id = "p2", Type = "process", Bounds = new DiagramRect(300, 400, 120, 60) });
        diagram.Add(new DiagramNode { Id = "blocked", Type = "server", Bounds = new DiagramRect(600, 400, 100, 50) });
        diagram.Add(new DiagramNode { Id = "tiny", Type = "small", Bounds = new DiagramRect(0, 600, 60, 60) });
        return diagram;
    }
}
