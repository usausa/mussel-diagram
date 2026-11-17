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
        GroupTypes = [new DiagramGroupType { Name = "vpc" }, new DiagramGroupType { Name = "team", Kind = DiagramGroupKind.Overlay }],
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
        var origin = cut.Instance.ScriptVersion;

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["vpc"], new DiagramPoint(10, 20), false, null, null, origin, 0));

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

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));

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
        var seen = cut.Instance.ScriptVersion;
        await cut.InvokeAsync(() => diagram.Move(["a"], new DiagramPoint(0, 5)));
        await cut.InvokeAsync(() => diagram.Replace((DiagramNode)diagram.Find("b")! with { Label = "B" }));

        var stale = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, seen, 0));
        var unrelated = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["c"], new DiagramPoint(10, 0), false, null, null, seen, stale.Rejections));

        Assert.False(stale.Accepted);
        Assert.Equal(MusselDiagramOptions.Default.Reason(DiagramReasonKind.Changed), stale.Reason);
        Assert.True(unrelated.Accepted);
    }

    [Fact]
    public async Task RejectionsUseTheTextsOfTheDiagram()
    {
        var options = new MusselDiagramOptions
        {
            Cultures = { [string.Empty] = new DiagramTexts { Reasons = new Dictionary<DiagramReasonKind, string> { [DiagramReasonKind.Changed] = "Moved by someone else" } } }
        };
        var diagram = CreateDiagram(options);
        var rejected = new List<DiagramCommitRejectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.CommitRejected, rejected.Add));
        var seen = cut.Instance.ScriptVersion;
        await cut.InvokeAsync(() => diagram.Move(["a"], new DiagramPoint(0, 5)));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, seen, 0));

        Assert.Equal("Moved by someone else", result.Reason);
        Assert.Equal("Moved by someone else", Assert.Single(rejected).Reason.Message);
    }

    [Fact]
    public async Task ManyWritesElsewhereDoNotRejectAMove()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var seen = cut.Instance.ScriptVersion;
        for (var i = 0; i < 70; i++)
        {
            await cut.InvokeAsync(() => diagram.Replace((DiagramNode)diagram.Find("b")! with { Label = $"B{i}" }, new DiagramWriteOptions { History = DiagramHistoryMode.Skip }));
        }

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, seen, 0));

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task LockedElementsAndReadOnlyViewsAreNotMoved()
    {
        var diagram = CreateDiagram();
        diagram.Replace((DiagramNode)diagram.Find("c")! with { Locked = true });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var version = diagram.Snapshot.Version;
        var origin = cut.Instance.ScriptVersion;

        var locked = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["c"], new DiagramPoint(10, 0), false, null, null, origin, 0));
        var typed = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["d"], new DiagramPoint(10, 0), false, null, null, origin, locked.Rejections));
        cut.Render(static parameters => parameters.Add(static p => p.ReadOnly, true));
        var readOnly = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, null, origin, typed.Rejections));

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
        var origin = cut.Instance.ScriptVersion;

        var first = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, origin, 0));
        var second = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, null, origin, 0));
        var third = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["b"], new DiagramPoint(10, 0), false, null, null, origin, second.Rejections));

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

    [Fact]
    public async Task WhatTheLastCommitChangedDoesNotRejectTheNextOne()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var start = cut.Instance.ScriptVersion;

        var first = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(350, 0), false, null, null, start, 0));
        var grown = ((DiagramBox)diagram.Find("vpc")!).Bounds;
        var second = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["vpc"], new DiagramPoint(10, 0), false, null, null, start, first.Rejections));

        Assert.True(first.Accepted);
        Assert.True(grown.Width > 400);
        Assert.True(second.Accepted);
        Assert.Equal(grown with { X = grown.X + 10 }, ((DiagramBox)diagram.Find("vpc")!).Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovingAnOverlayIsRejectedWhenItsMembersChanged(bool addMember)
    {
        var diagram = CreateDiagram();
        diagram.Add(new DiagramNode { Id = "e", Type = "server", Bounds = new DiagramRect(620, 150, 40, 40) });
        diagram.Add(new DiagramNode { Id = "f", Type = "server", Bounds = new DiagramRect(640, 220, 40, 40) });
        diagram.Add(new DiagramGroup { Id = "team", Type = "team", Kind = DiagramGroupKind.Overlay, MemberIds = ["b", "c", "e"] });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var start = cut.Instance.ScriptVersion;
        var frame = ((DiagramBox)diagram.Find("team")!).Bounds;
        await cut.InvokeAsync(() =>
        {
            if (addMember)
            {
                diagram.Replace((DiagramGroup)diagram.Find("team")! with { MemberIds = ["b", "c", "e", "f"] });
            }
            else
            {
                diagram.Move(["e"], new DiagramPoint(5, 5));
            }
        });
        Assert.Equal(frame, ((DiagramBox)diagram.Find("team")!).Bounds);

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["team"], new DiagramPoint(10, 0), false, null, null, start, 0));

        Assert.False(result.Accepted);
        Assert.Equal(MusselDiagramOptions.Default.Reason(DiagramReasonKind.Changed), result.Reason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ASplitIsRejectedWhenTheLinkChanged(bool drop)
    {
        var diagram = CreateDiagram();
        diagram.Add(new DiagramNode { Id = "e", Type = "server", Bounds = new DiagramRect(300, 400, 100, 50) });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var start = cut.Instance.ScriptVersion;
        await cut.InvokeAsync(() =>
        {
            if (drop)
            {
                diagram.Reconnect("ab", DiagramLinkEndSide.Target, new DiagramLinkEnd("c"));
            }
            else
            {
                diagram.Delete(["ab"]);
            }
        });
        var version = diagram.Snapshot.Version;

        var result = drop
            ? await cut.InvokeAsync(() => cut.Instance.CommitDropAsync("server", new DiagramRect(300, 0, 100, 50), null, null, "ab", start, 0))
            : await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["e"], new DiagramPoint(0, -300), false, null, "ab", start, 0));

        Assert.False(result.Accepted);
        Assert.Equal(MusselDiagramOptions.Default.Reason(DiagramReasonKind.Changed), result.Reason);
        Assert.Equal(version, diagram.Snapshot.Version);
    }

    [Fact]
    public async Task AReconnectIsRejectedWhenTheEndsOfTheLinkChanged()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var start = cut.Instance.ScriptVersion;
        await cut.InvokeAsync(() => diagram.Reconnect("ab", DiagramLinkEndSide.Target, new DiagramLinkEnd("c")));
        var version = diagram.Snapshot.Version;

        var result = await cut.InvokeAsync(() => cut.Instance.CommitReconnectAsync("ab", DiagramLinkEndSide.Source, new DiagramLinkEnd("b"), start, 0));

        Assert.False(result.Accepted);
        Assert.Equal(MusselDiagramOptions.Default.Reason(DiagramReasonKind.Changed), result.Reason);
        Assert.Equal(version, diagram.Snapshot.Version);
    }

    [Fact]
    public async Task ChangesBesideWhatAMoveWritesDoNotRejectIt()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var start = cut.Instance.ScriptVersion;
        await cut.InvokeAsync(() => diagram.Replace((DiagramNode)diagram.Find("a")! with { Label = "A", Look = new DiagramBoxLook { Fill = "#ff0000" } }));
        await cut.InvokeAsync(() => diagram.Move(["b"], new DiagramPoint(0, 5)));
        await cut.InvokeAsync(() => diagram.LoadJson(diagram.ToJson()));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["vpc"], new DiagramPoint(10, 0), false, null, null, start, 0));

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task CommitsAndCommandsWithoutASelectionUseTheLastOneFromTheScript()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => cut.Instance.OnSelectionChangedAsync(["b"]));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["c"]).AsTask());

        var result = await cut.InvokeAsync(() => cut.Instance.CommitMoveAsync(["a"], new DiagramPoint(10, 0), false, null, null, cut.Instance.ScriptVersion, 0));
        var selected = cut.Instance.SelectedIds;
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["c"]).AsTask());
        await cut.InvokeAsync(() => cut.Instance.ExecuteFromScriptAsync(DiagramCommands.Delete, null));

        Assert.True(result.Accepted);
        Assert.Equal(["b"], selected);
        Assert.False(diagram.Snapshot.Contains("b"));
        Assert.True(diagram.Snapshot.Contains("c"));
    }

    private static Diagram CreateDiagram(MusselDiagramOptions? options = null)
    {
        var diagram = new Diagram(Schema) { Options = options };
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 400, 300) });
        diagram.Add(new DiagramNode { Id = "a", Type = "server", Bounds = new DiagramRect(40, 60, 100, 50), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "b", Type = "server", Bounds = new DiagramRect(600, 60, 100, 50) });
        diagram.Add(new DiagramNode { Id = "c", Type = "server", Bounds = new DiagramRect(600, 300, 100, 50) });
        diagram.Add(new DiagramNode { Id = "d", Type = "fixed", Bounds = new DiagramRect(800, 300, 100, 50) });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "arrow", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        return diagram;
    }
}
