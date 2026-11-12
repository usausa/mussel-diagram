namespace MusselDiagram;

using MusselDiagram.Internal;

using static MusselDiagram.TestDiagrams;

public sealed class RoutingTests
{
    [Fact]
    public void RoutesGoAroundNodesTheSameWayEachTime()
    {
        var first = CreateBlocked();
        var second = CreateBlocked();

        var route = Route(first, "ab");

        Assert.Equal(new DiagramPoint(100, 25), route[0]);
        Assert.Equal(new DiagramPoint(400, 25), route[^1]);
        Assert.False(OrthogonalRouter.Crosses(route, new DiagramRect(190, -60, 70, 170)));
        Assert.True(route.Length > 2);
        Assert.Equal(route, Route(second, "ab"));
    }

    [Fact]
    public void BendsOfSimpleRoutesLieOnTheGrid()
    {
        var onGrid = new Diagram(Schema);
        var free = new Diagram(Schema with { GridSize = 0 });
        foreach (var diagram in new[] { onGrid, free })
        {
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 403, 100));
            diagram.Connect(Link("ab", "a", "b"));
        }

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(250, 25), new DiagramPoint(250, 125), new DiagramPoint(403, 125)], Route(onGrid, "ab"));
        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(251.5, 25), new DiagramPoint(251.5, 125), new DiagramPoint(403, 125)], Route(free, "ab"));
    }

    [Fact]
    public void BendsStayInTheMiddleWhenNoGridLineIsBetweenTheStubs()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0) with { Bounds = new DiagramRect(0, 0, 101, 50) });
        diagram.Add(Node("b", 145, 30));

        diagram.Connect(Link("ab", "a", "b"));

        Assert.Equal([new DiagramPoint(101, 25), new DiagramPoint(123, 25), new DiagramPoint(123, 55), new DiagramPoint(145, 55)], Route(diagram, "ab"));
    }

    [Fact]
    public void RoutesAroundNodesTurnOnTheGrid()
    {
        var wall = new DiagramRect(183, -37, 94, 121);
        var onGrid = new Diagram(Schema);
        var free = new Diagram(Schema with { GridSize = 0 });
        foreach (var diagram in new[] { onGrid, free })
        {
            diagram.Add(Node("a", 0, -5));
            diagram.Add(Node("b", 400, -5));
            diagram.Add(Node("wall", 0, 0) with { Bounds = wall });
            diagram.Connect(Link("ab", "a", "b"));
        }

        var route = Route(onGrid, "ab");
        Assert.True(route.Length > 2);
        Assert.False(OrthogonalRouter.Crosses(route, wall));
        Assert.All(route, static x => Assert.Equal((0d, 0d), (x.X % 10, x.Y % 10)));
        Assert.Contains(Route(free, "ab"), static x => (x.Y % 10) != 0);
    }

    [Fact]
    public void RouteWithoutAWayAroundCrossesTheNodes()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));
        diagram.Add(Node("wall", 110, -500) with { Bounds = new DiagramRect(110, -500, 280, 1000) });

        diagram.Connect(Link("ab", "a", "b"));

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(400, 25)], Route(diagram, "ab"));
        Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("ab")!).RouteState);
    }

    [Fact]
    public void LinksCrossingAChangedNodeAreRoutedAgain()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));
        diagram.Add(Node("c", 200, 300));
        diagram.Add(Node("d", 0, 600));
        diagram.Add(Node("e", 400, 600));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Connect(Link("de", "d", "e"));
        var far = diagram.Find("de");

        diagram.Move(["c"], new DiagramPoint(0, -300));

        Assert.True(Route(diagram, "ab").Length > 2);
        Assert.Same(far, diagram.Find("de"));

        diagram.Delete(["c"]);

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(400, 25)], Route(diagram, "ab"));
    }

    [Fact]
    public void ResizeChangesTheBoundsAndTheLinks()
    {
        var diagram = new Diagram(Schema with
        {
            GroupTypes = [.. Schema.GroupTypes, new DiagramGroupType { Name = "fit", Sizing = DiagramGroupSizing.Content }]
        });
        diagram.Add(Group("vpc", 0, 0, 600, 400));
        diagram.Add(Node("a", 40, 80, "vpc"));
        diagram.Add(Node("b", 400, 80, "vpc"));
        diagram.Connect(Link("ab", "a", "b"));
        diagram.Add(new DiagramGroup { Id = "fit", Type = "fit", Bounds = new DiagramRect(1000, 0, 100, 100) });
        var version = diagram.Snapshot.Version;

        diagram.Resize("a", new DiagramRect(40, 80, 200, 100));
        Assert.Equal(new DiagramRect(40, 80, 200, 100), Bounds(diagram, "a"));
        Assert.Equal(new DiagramPoint(240, 130), Route(diagram, "ab")[0]);

        diagram.Resize("vpc", new DiagramRect(0, 0, 100, 100));
        Assert.Equal(new DiagramRect(0, 0, 520, 200), Bounds(diagram, "vpc"));

        diagram.Resize("fit", new DiagramRect(0, 0, 10, 10));
        Assert.Equal(version + 2, diagram.Snapshot.Version);
        Assert.Throws<DiagramException>(() => diagram.Resize("a", new DiagramRect(0, 0, 0, 10)));
        Assert.Throws<DiagramException>(() => diagram.Resize("ab", new DiagramRect(0, 0, 10, 10)));
    }

    [Fact]
    public void ReconnectMovesOneEnd()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));
        diagram.Add(Node("s", 0, 300, type: "service"));
        diagram.Connect(Link("ab", "a", "b"));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Reconnect("ab", DiagramLinkEndSide.Target, new DiagramLinkEnd("s", "in"));

        var link = (DiagramLink)diagram.Find("ab")!;
        Assert.Equal(new DiagramLinkEnd("s", "in"), link.Target);
        Assert.Equal(new DiagramPoint(0, 325), link.Route[^1]);
        Assert.Equal(DiagramChangeParts.Ends | DiagramChangeParts.Route, change!.GetChanges("ab"));
        Assert.Throws<DiagramException>(() => diagram.Reconnect("ab", DiagramLinkEndSide.Source, new DiagramLinkEnd("s", "missing")));
        Assert.Throws<DiagramException>(() => diagram.Reconnect("a", DiagramLinkEndSide.Source, new DiagramLinkEnd("b")));
    }

    [Fact]
    public void CurvedLinksHaveTwoControlPoints()
    {
        var diagram = new Diagram(Schema with { LinkTypes = [.. Schema.LinkTypes, new DiagramLinkType { Name = "curve", Form = DiagramLineForm.Curved, TargetMarker = DiagramMarker.None }] });
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));

        diagram.Connect(Link("ab", "a", "b", type: "curve"));

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(250, 25), new DiagramPoint(250, 25), new DiagramPoint(400, 25)], Route(diagram, "ab"));
        Assert.Equal("M100,25C250,25 250,25 400,25", diagram.GetLinkGeometry("ab")!.Path);
    }

    [Fact]
    public void SynchronousRoutersGiveTheRouteAtOnce()
    {
        var router = new FixedRouter();
        var diagram = new Diagram(Schema with { Router = router });
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));
        diagram.Add(Node("c", 200, 300));

        diagram.Connect(Link("ab", "a", "b"));
        router.Error = "No way";
        diagram.Connect(Link("ba", "b", "a"));

        var request = Assert.Single(router.Requests, x => x.Source.Position == new DiagramPoint(100, 25));
        Assert.Equal(DiagramSide.Right, request.Source.Direction);
        Assert.Equal([new DiagramRect(200, 300, 100, 50)], request.Obstacles);
        Assert.Equal("arrow", request.LinkType!.Name);
        Assert.Equal(10, request.GridSize);
        var ab = (DiagramLink)diagram.Find("ab")!;
        Assert.Equal(DiagramRouteState.Routed, ab.RouteState);
        Assert.Equal(FixedRouter.Points, ab.Route);
        var ba = (DiagramLink)diagram.Find("ba")!;
        Assert.Equal(DiagramRouteState.Failed, ba.RouteState);
        Assert.Equal("No way", ba.RouteError);
        Assert.Equal(2, ba.Route.Length);
    }

    [Fact]
    public void AsynchronousRoutesReplaceTheSimpleRoute()
    {
        RunWith(static context =>
        {
            var router = new ManualRouter();
            var diagram = new Diagram(Schema with { Router = router });
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Connect(Link("ab", "a", "b"));
            Assert.Equal(DiagramRouteState.Provisional, LinkOf(diagram).RouteState);

            router.Complete(0, [new DiagramPoint(100, 25), new DiagramPoint(250, 100), new DiagramPoint(400, 25)]);
            context.RunOne();

            Assert.Equal(DiagramRouteState.Routed, LinkOf(diagram).RouteState);
            Assert.Equal(3, LinkOf(diagram).Route.Length);
            diagram.Undo();
            Assert.False(diagram.Snapshot.Contains("ab"));
            diagram.Redo();
            Assert.Equal(3, LinkOf(diagram).Route.Length);
        });
    }

    [Fact]
    public void LateRoutesAreThrownAwayWhenTheEndsMoved()
    {
        RunWith(static context =>
        {
            var router = new ManualRouter();
            var diagram = new Diagram(Schema with { Router = router });
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Connect(Link("ab", "a", "b"));
            diagram.Move(["b"], new DiagramPoint(0, 200));
            Assert.Equal(2, router.Count);
            Assert.True(router.Tokens[0].IsCancellationRequested);
            Assert.False(router.Tokens[1].IsCancellationRequested);
            var version = diagram.Snapshot.Version;

            router.Complete(0, [new DiagramPoint(0, 0), new DiagramPoint(1, 1)]);
            context.RunOne();
            Assert.Equal(version, diagram.Snapshot.Version);

            router.Complete(1, [new DiagramPoint(100, 25), new DiagramPoint(250, 225), new DiagramPoint(400, 225)]);
            context.RunOne();
            Assert.Equal(new DiagramPoint(250, 225), LinkOf(diagram).Route[1]);
            Assert.Equal(2, router.Count);
        });
    }

    [Fact]
    public void RoutesWaitingAtUndoAreAskedForAgainAtRedo()
    {
        RunWith(static context =>
        {
            var router = new ManualRouter();
            var diagram = new Diagram(Schema with { Router = router });
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Connect(Link("ab", "a", "b"));

            diagram.Undo();

            Assert.True(router.Tokens[0].IsCancellationRequested);

            diagram.Redo();

            Assert.Equal(2, router.Count);
            Assert.Equal(DiagramRouteState.Provisional, LinkOf(diagram).RouteState);
            router.Complete(1, [new DiagramPoint(100, 25), new DiagramPoint(250, 100), new DiagramPoint(400, 25)]);
            context.RunOne();
            Assert.Equal(DiagramRouteState.Routed, LinkOf(diagram).RouteState);
            Assert.Equal(3, LinkOf(diagram).Route.Length);
        });
    }

    [Fact]
    public void RoutesOverTheBudgetFollowInSteps()
    {
        RunWith(static context =>
        {
            var diagram = new Diagram(Schema) { RouteLimit = TimeSpan.Zero };
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Add(Node("x", 200, -50) with { Bounds = new DiagramRect(200, -50, 50, 150) });
            diagram.Add(Node("c", 0, 300));
            diagram.Add(Node("d", 400, 300));
            using (diagram.BeginUpdate())
            {
                diagram.Connect(Link("ab", "a", "b"));
                diagram.Connect(Link("cd", "c", "d"));
            }

            Assert.Equal(DiagramRouteState.Provisional, LinkOf(diagram).RouteState);
            Assert.Equal(2, LinkOf(diagram).Route.Length);
            Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("cd")!).RouteState);
            var version = diagram.Snapshot.Version;

            context.RunOne();

            Assert.Equal(DiagramRouteState.Routed, LinkOf(diagram).RouteState);
            Assert.True(LinkOf(diagram).Route.Length > 2);
            Assert.Equal(version + 1, diagram.Snapshot.Version);
            diagram.Undo();
            Assert.Empty(diagram.Snapshot.Links);
            diagram.Redo();
            Assert.True(LinkOf(diagram).Route.Length > 2);
        });
    }

    [Fact]
    public void EachStepLeavesTheRestForTheNextStep()
    {
        RunWith(static context =>
        {
            var diagram = new Diagram(Schema) { RouteLimit = TimeSpan.Zero, BackgroundRouteLimit = TimeSpan.Zero, PublishInterval = TimeSpan.Zero };
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Add(Node("x", 200, -50) with { Bounds = new DiagramRect(200, -50, 50, 150) });
            diagram.Add(Node("c", 0, 300));
            diagram.Add(Node("d", 400, 300));
            diagram.Add(Node("y", 200, 250) with { Bounds = new DiagramRect(200, 250, 50, 150) });
            using (diagram.BeginUpdate())
            {
                diagram.Connect(Link("ab", "a", "b"));
                diagram.Connect(Link("cd", "c", "d"));
            }

            context.RunOne();

            Assert.Single(diagram.Snapshot.Links, static x => x.RouteState == DiagramRouteState.Provisional);

            context.RunOne();

            Assert.All(diagram.Snapshot.Links, static x => Assert.True((x.RouteState == DiagramRouteState.Routed) && (x.Route.Length > 2)));
        });
    }

    [Fact]
    public void WaitingRoutesThatANewNodeBlocksAreRoutedAgain()
    {
        RunWith(static context =>
        {
            var blocker = OnDetour(Route(CreateBlocked(), "ab"));
            var diagram = new Diagram(Schema) { RouteLimit = TimeSpan.Zero, BackgroundRouteLimit = TimeSpan.Zero, PublishInterval = TimeSpan.FromHours(1) };
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Add(Node("x", 200, -50) with { Bounds = new DiagramRect(200, -50, 50, 150) });
            diagram.Add(Node("c", 0, 300));
            diagram.Add(Node("d", 400, 300));
            diagram.Add(Node("y", 200, 250) with { Bounds = new DiagramRect(200, 250, 50, 150) });
            using (diagram.BeginUpdate())
            {
                diagram.Connect(Link("ab", "a", "b"));
                diagram.Connect(Link("cd", "c", "d"));
            }

            context.RunOne();
            diagram.Add(Node("z", 0, 0) with { Bounds = blocker });
            context.RunOne();

            Assert.Equal(DiagramRouteState.Provisional, LinkOf(diagram).RouteState);
            Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("cd")!).RouteState);

            context.RunOne();

            Assert.Equal(DiagramRouteState.Routed, LinkOf(diagram).RouteState);
            Assert.False(OrthogonalRouter.Crosses(Route(diagram, "ab"), blocker));
        });
    }

    [Fact]
    public void RoutesFromLaterStepsStayOutOfStepsWithOtherNodes()
    {
        RunWith(static context =>
        {
            var blocker = OnDetour(Route(CreateBlocked(), "ab"));
            var diagram = new Diagram(Schema) { RouteLimit = TimeSpan.Zero };
            diagram.Add(Node("a", 0, 0));
            diagram.Add(Node("b", 400, 0));
            diagram.Add(Node("x", 200, -50) with { Bounds = new DiagramRect(200, -50, 50, 150) });
            diagram.Add(Node("w", 0, 0) with { Bounds = blocker });
            diagram.Connect(Link("ab", "a", "b"));
            diagram.Move(["w"], new DiagramPoint(0, 1000));
            context.RunOne();

            diagram.Undo();
            context.RunOne();

            Assert.Equal(DiagramRouteState.Routed, LinkOf(diagram).RouteState);
            Assert.False(OrthogonalRouter.Crosses(Route(diagram, "ab"), blocker));
        });
    }

    [Fact]
    public void LoadedRoutesOverTheBudgetFollowInSteps()
    {
        RunWith(static context =>
        {
            var source = CreateBlocked();
            var json = source.ToJson().Replace("\"route\":", "\"ignored\":", StringComparison.Ordinal);
            var diagram = new Diagram(Schema) { RouteLimit = TimeSpan.Zero };

            diagram.LoadJson(json);

            Assert.Equal(DiagramRouteState.Provisional, LinkOf(diagram).RouteState);
            context.RunOne();
            Assert.Equal(Route(source, "ab"), LinkOf(diagram).Route);
            Assert.False(diagram.CanUndo);
        });
    }

    private static Diagram CreateBlocked()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 400, 0));
        diagram.Add(Node("x", 200, -50) with { Bounds = new DiagramRect(200, -50, 50, 150) });
        diagram.Connect(Link("ab", "a", "b"));
        return diagram;
    }

    private static DiagramRect OnDetour(ImmutableArray<DiagramPoint> route)
    {
        var longest = Enumerable.Range(1, route.Length - 1).MaxBy(i => Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y));
        var middle = new DiagramPoint((route[longest].X + route[longest - 1].X) / 2, (route[longest].Y + route[longest - 1].Y) / 2);
        return new DiagramRect(middle.X - 10, middle.Y - 10, 20, 20);
    }

    private static void RunWith(Action<QueueContext> test)
    {
        var previous = SynchronizationContext.Current;
        var context = new QueueContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            test(context);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static DiagramLink LinkOf(Diagram diagram) => (DiagramLink)diagram.Find("ab")!;

    private static ImmutableArray<DiagramPoint> Route(Diagram diagram, string id) => ((DiagramLink)diagram.Find(id)!).Route;

    private static DiagramRect Bounds(Diagram diagram, string id) => ((DiagramBox)diagram.Find(id)!).Bounds;

    private sealed class FixedRouter : IDiagramRouter
    {
        public static readonly ImmutableArray<DiagramPoint> Points = [new(100, 25), new(250, 25), new(250, 100), new(400, 100)];

        public List<DiagramRouteRequest> Requests { get; } = [];

        public string? Error { get; set; }

        public ValueTask<DiagramRouteResult> RouteAsync(DiagramRouteRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(Error is null ? new DiagramRouteResult { Points = Points } : new DiagramRouteResult { Error = Error });
        }
    }

    private sealed class QueueContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> queue = new();

        private readonly object gate = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (gate)
            {
                queue.Enqueue((d, state));
                Monitor.PulseAll(gate);
            }
        }

        public void RunOne()
        {
            (SendOrPostCallback Callback, object? State) item;
            lock (gate)
            {
                while (queue.Count == 0)
                {
                    if (!Monitor.Wait(gate, TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException();
                    }
                }

                item = queue.Dequeue();
            }

            item.Callback(item.State);
        }
    }

    private sealed class ManualRouter : IDiagramRouter
    {
        private readonly List<TaskCompletionSource<DiagramRouteResult>> calls = [];

        public int Count => calls.Count;

        public List<CancellationToken> Tokens { get; } = [];

        public ValueTask<DiagramRouteResult> RouteAsync(DiagramRouteRequest request, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<DiagramRouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            calls.Add(completion);
            Tokens.Add(cancellationToken);
            return new ValueTask<DiagramRouteResult>(completion.Task);
        }

        public void Complete(int call, ImmutableArray<DiagramPoint> points) => calls[call].SetResult(new DiagramRouteResult { Points = points });
    }
}
