namespace MusselDiagram;

public sealed class CommitTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "server" },
            new DiagramNodeType { Name = "fixed", Permissions = DiagramPermissions.Select }
        ],
        GroupTypes = [new DiagramGroupType { Name = "vpc" }],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }]
    };

    public CommitTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public async Task AcceptedMoveIsWrittenAsTheUsersChange()
    {
        var diagram = CreateDiagram();
        var origins = new List<DiagramChangeOrigin>();
        diagram.Changed += (_, e) => origins.Add(e.Origin);
        var moved = new List<DiagramMovedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moved, moved.Add));
        var version = diagram.Snapshot.Version;

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["vpc"], new DiagramPoint(10, 20), false, null, version, 0));

        Assert.True(result.Accepted);
        Assert.Equal(version + 1, result.Version);
        Assert.Equal(new DiagramRect(50, 80, 100, 50), ((DiagramBox)diagram.Find("a")!).Bounds);
        Assert.Equal([DiagramChangeOrigin.User], origins);
        var e = Assert.Single(moved);
        Assert.Equal(["vpc"], e.ElementIds);
        Assert.Equal(new DiagramPoint(10, 20), e.Offset);
        Assert.Same(diagram.Snapshot, e.Snapshot);
        Assert.Equal("translate(50,80)", cut.Find("g.mussel-diagram-node[data-id='a']").GetAttribute("transform"));
    }

    [Fact]
    public async Task MovingCanCancelWithAReason()
    {
        var diagram = CreateDiagram();
        var rejected = new List<DiagramCommitRejectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moving, static e =>
            {
                e.Cancel = true;
                e.Reason = "Not now";
            })
            .Add(p => p.CommitRejected, rejected.Add));
        var before = diagram.Snapshot;

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, before.Version, 0));

        Assert.False(result.Accepted);
        Assert.Equal("Not now", result.Reason);
        Assert.Equal(1, result.Rejections);
        Assert.Same(before, diagram.Snapshot);
        var e = Assert.Single(rejected);
        Assert.Equal(DiagramOperation.Move, e.Operation);
        Assert.Equal(DiagramReasonKind.App, e.Reason.Kind);
    }

    [Fact]
    public async Task MovesFromAnOlderVersionAreRejectedWhenTheElementsChanged()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var seen = diagram.Snapshot.Version;
        await cut.InvokeAsync(() => diagram.Move(["a"], new DiagramPoint(0, 5)));
        await cut.InvokeAsync(() => diagram.Replace((DiagramNode)diagram.Find("b")! with { Label = "B" }));

        var stale = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, seen, 0));
        var unrelated = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["c"], new DiagramPoint(10, 0), false, null, seen, stale.Rejections));

        Assert.False(stale.Accepted);
        Assert.Equal(MusselDiagram.Internal.BuiltInTexts.Reason(DiagramReasonKind.Changed), stale.Reason);
        Assert.True(unrelated.Accepted);
    }

    [Fact]
    public async Task LockedElementsAndReadOnlyViewsAreNotMoved()
    {
        var diagram = CreateDiagram();
        diagram.Replace((DiagramNode)diagram.Find("c")! with { Locked = true });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var version = diagram.Snapshot.Version;

        var locked = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["c"], new DiagramPoint(10, 0), false, null, version, 0));
        var typed = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["d"], new DiagramPoint(10, 0), false, null, version, locked.Rejections));
        cut.Render(static parameters => parameters.Add(static p => p.ReadOnly, true));
        var readOnly = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, version, typed.Rejections));

        Assert.False(locked.Accepted);
        Assert.False(typed.Accepted);
        Assert.False(readOnly.Accepted);
        Assert.Equal(version, diagram.Snapshot.Version);
    }

    [Fact]
    public async Task CommitsSentBeforeAKnownRejectionAreRejectedSilently()
    {
        var diagram = CreateDiagram();
        var decisions = new Queue<bool>([true, false]);
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Moving, e => e.Cancel = decisions.Dequeue()));
        var version = diagram.Snapshot.Version;

        var first = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, version, 0));
        var second = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, version, 0));
        var third = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, version, second.Rejections));

        Assert.False(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Null(second.Reason);
        Assert.True(third.Accepted);
    }

    [Fact]
    public async Task SelectionFollowsTheScriptAndDropsDeletedElements()
    {
        var diagram = CreateDiagram();
        var reported = new List<IReadOnlyList<string>>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.SelectionChanged, reported.Add));

        await cut.InvokeAsync(() => cut.Instance.OnSelectionChangedAsync(["a", "missing", "b"]));
        Assert.Equal(["a", "b"], cut.Instance.SelectedIds);

        await cut.InvokeAsync(() => diagram.Delete(["a"]));
        Assert.Equal(["b"], cut.Instance.SelectedIds);

        await cut.InvokeAsync(() => cut.Instance.ClearSelectionAsync().AsTask());
        Assert.Empty(cut.Instance.SelectedIds);
        Assert.Equal(3, reported.Count);
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 400, 300) });
        diagram.Add(new DiagramNode { Id = "a", Type = "server", Bounds = new DiagramRect(40, 60, 100, 50), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "b", Type = "server", Bounds = new DiagramRect(600, 60, 100, 50) });
        diagram.Add(new DiagramNode { Id = "c", Type = "server", Bounds = new DiagramRect(600, 300, 100, 50) });
        diagram.Add(new DiagramNode { Id = "d", Type = "fixed", Bounds = new DiagramRect(800, 300, 100, 50) });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "arrow", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        return diagram;
    }
}
