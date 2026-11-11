namespace MusselDiagram;

public sealed class CommandTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "server",
                DisplayName = "Server",
                Category = "compute",
                CreateContent = static c => new DiagramContent { Label = c.Value ?? "New server" }
            },
            new DiagramNodeType { Name = "note", DisplayName = "Note", Slots = [new DiagramLabelSlot { Name = "title" }, new DiagramLabelSlot { Name = "body", Editable = false }] },
            new DiagramNodeType { Name = "database", DisplayName = "Database", Category = "data", SearchWords = ["rds"] }
        ],
        GroupTypes = [new DiagramGroupType { Name = "vpc", DisplayName = "VPC", Category = "network" }],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        Categories = [new DiagramCategory { Name = "network", DisplayName = "Network" }, new DiagramCategory { Name = "data", DisplayName = "Data", Collapsed = true }],
        DefaultLinkType = "arrow",
        DefaultGroupType = "vpc"
    };

    public CommandTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void KeysBelongToTheLastCommandThatTookThem()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var commands = cut.Instance.Commands;

        commands.Add(new DiagramCommand { Name = "Mine", Keys = ["ctrl+z"], Execute = static _ => Task.CompletedTask });

        Assert.Equal(["Ctrl+Z"], commands["Mine"].Keys);
        Assert.Empty(commands[DiagramCommands.Undo].Keys);
        commands.SetKeys(DiagramCommands.Undo, ["Alt+Shift+u"]);
        Assert.Equal(["Alt+Shift+U"], commands[DiagramCommands.Undo].Keys);
        Assert.True(commands.Remove("Mine"));
        Assert.False(commands.TryGet("Mine", out _));
        Assert.Equal("Bring to front", commands[DiagramCommands.BringToFront].DisplayName);
    }

    [Fact]
    public async Task ChecksAndActionsCanBeChanged()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var commands = cut.Instance.Commands;
        var copied = 0;
        var changes = 0;
        commands.Changed += (_, _) => changes++;
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());
        Assert.True(commands[DiagramCommands.Delete].IsEnabled);
        Assert.True(changes > 0);

        commands.SetCanExecute(DiagramCommands.Delete, static c => !c.SelectedIds.Contains("a"));
        commands.SetExecute(DiagramCommands.Copy, _ =>
        {
            copied++;
            return Task.CompletedTask;
        });
        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.Delete));
        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.Copy));

        Assert.False(commands[DiagramCommands.Delete].IsEnabled);
        Assert.True(diagram.Snapshot.Contains("a"));
        Assert.Equal(1, copied);
        Assert.False(commands[DiagramCommands.Paste].IsEnabled);
    }

    [Fact]
    public async Task ArrangeOrderAndLockCommandsWriteOneChangeEach()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var commands = cut.Instance.Commands;
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a", "b"]).AsTask());
        var version = diagram.Snapshot.Version;

        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.AlignTop));
        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.BringToFront));
        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.ToggleLock));

        Assert.Equal(version + 3, diagram.Snapshot.Version);
        Assert.Equal(0, ((DiagramBox)diagram.Find("b")!).Bounds.Y);
        Assert.Equal(["n", "a", "b"], diagram.Snapshot.Nodes.Select(static x => x.Id));
        Assert.True(((DiagramBox)diagram.Find("a")!).Locked);
        Assert.True(((DiagramBox)diagram.Find("b")!).Locked);
        Assert.True(commands[DiagramCommands.ToggleLock].IsChecked);
        Assert.False(commands[DiagramCommands.AlignLeft].IsEnabled);
    }

    [Fact]
    public async Task DeletingAndPastingRaiseTheirEvents()
    {
        var diagram = CreateDiagram();
        var deleting = new List<DiagramDeletingEventArgs>();
        var pasted = new List<DiagramPastedEventArgs>();
        var rejected = new List<DiagramCommitRejectedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Deleting, e =>
            {
                deleting.Add(e);
                e.Cancel = deleting.Count == 1;
                e.Reason = "Keep it";
            })
            .Add(p => p.Pasted, pasted.Add)
            .Add(p => p.CommitRejected, rejected.Add));
        var commands = cut.Instance.Commands;
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.Delete));
        Assert.True(diagram.Snapshot.Contains("a"));
        Assert.Equal(["a", "ab"], deleting[0].ElementIds.Order(StringComparer.Ordinal));
        Assert.Equal(DiagramReasonKind.App, rejected[0].Reason.Kind);

        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.Cut));
        Assert.False(diagram.Snapshot.Contains("a"));
        Assert.True(deleting[1].IsCut);

        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.Paste));
        var copy = Assert.IsType<DiagramNode>(diagram.Find(pasted[0].IdMap["a"]));
        Assert.Equal(new DiagramRect(20, 20, 100, 50), copy.Bounds);
        Assert.Equal([copy.Id], cut.Instance.SelectedIds);
    }

    [Fact]
    public async Task TextCommitsWriteTheSlot()
    {
        var diagram = CreateDiagram();
        var committed = new List<DiagramTextCommittedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.TextCommitted, committed.Add));
        var version = diagram.Snapshot.Version;

        var label = await cut.InvokeAsync(() => cut.Instance.CommitTextAsync("n", "title", "Hello", version, 0));
        var same = await cut.InvokeAsync(() => cut.Instance.CommitTextAsync("n", "title", "Hello", diagram.Snapshot.Version, 0));
        var fixedSlot = await cut.InvokeAsync(() => cut.Instance.CommitTextAsync("n", "body", "No", diagram.Snapshot.Version, 0));
        var stale = await cut.InvokeAsync(() => cut.Instance.CommitTextAsync("n", "title", "Old", version, fixedSlot.Rejections));

        Assert.True(label.Accepted);
        Assert.True(same.Accepted);
        Assert.Equal(version + 1, diagram.Snapshot.Version);
        Assert.Equal("Hello", ((DiagramBox)diagram.Find("n")!).Label);
        Assert.Equal((null, "Hello"), (committed[0].OldText, committed[0].NewText));
        Assert.Single(committed);
        Assert.False(fixedSlot.Accepted);
        Assert.False(stale.Accepted);
    }

    [Fact]
    public async Task DropsUseTheContentFunctionAndSplitLinks()
    {
        var diagram = CreateDiagram();
        var dropped = new List<DiagramDroppedEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Dropped, dropped.Add));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitDropAsync("server", new DiagramRect(180, 0, 100, 50), null, "From list", "ab", diagram.Snapshot.Version, 0));

        Assert.True(result.Accepted);
        var node = (DiagramNode)diagram.Find(result.ElementId!)!;
        Assert.Equal("From list", node.Label);
        Assert.Equal("ab", dropped[0].SplitLinkId);
        Assert.Equal(new DiagramLinkEnd(node.Id), ((DiagramLink)diagram.Find("ab")!).Target);
        Assert.Contains(diagram.Snapshot.Links, x => (x.Source == new DiagramLinkEnd(node.Id)) && (x.Target == new DiagramLinkEnd("b")));
        diagram.Undo();
        Assert.False(diagram.Snapshot.Contains(node.Id));
        Assert.Equal(new DiagramLinkEnd("b"), ((DiagramLink)diagram.Find("ab")!).Target);
    }

    [Fact]
    public async Task ClicksAreReported()
    {
        var clicked = new List<DiagramElementPointerEventArgs>();
        var background = new List<DiagramPointerEventArgs>();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(p => p.ElementClicked, clicked.Add)
            .Add(p => p.BackgroundDoubleClicked, background.Add));

        await cut.InvokeAsync(() => cut.Instance.OnClickedAsync("a", new DiagramPoint(10, 20), true, false, false, false, false));
        await cut.InvokeAsync(() => cut.Instance.OnClickedAsync(null, new DiagramPoint(5, 6), false, false, false, false, true));

        Assert.Equal(("a", new DiagramPoint(10, 20), true), (clicked[0].ElementId, clicked[0].Position, clicked[0].ShiftKey));
        Assert.Equal(new DiagramPoint(5, 6), background[0].Position);
    }

    [Fact]
    public void ThePaletteListsTypesByCategoryAndSearches()
    {
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

        Assert.Equal(["Note", "Network", "VPC", "Data", "compute", "Server"], palette.FindAll(".mussel-diagram-palette-item, .mussel-diagram-palette-header").Select(static x => x.TextContent.Trim()));
        palette.Find(".mussel-diagram-palette-header[aria-expanded='false']").Click();
        Assert.Contains(palette.FindAll(".mussel-diagram-palette-item"), static x => x.GetAttribute("data-mussel-type") == "database");

        palette.Find("input").Input("rds");
        Assert.Equal(["Data", "Database"], palette.FindAll(".mussel-diagram-palette-item, .mussel-diagram-palette-header").Select(static x => x.TextContent.Trim()));
        palette.Find("input").Input("nothing");
        Assert.Equal("No matches", palette.Find(".mussel-diagram-palette-empty").TextContent);
    }

    [Fact]
    public void ThePaletteHonorsTheNormalizerAndTheReadOnlyView()
    {
        var view = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(static p => p.ReadOnly, true)
            .Add(static p => p.SearchNormalizer, static x => x.Replace("ｓ", "s", StringComparison.Ordinal)));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

        palette.Find("input").Input("ｓerver");

        var item = palette.Find(".mussel-diagram-palette-item");
        Assert.Equal("server", item.GetAttribute("data-mussel-type"));
        Assert.True(item.HasAttribute("data-disabled"));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "a", Type = "server", Bounds = new DiagramRect(0, 0, 100, 50) });
        diagram.Add(new DiagramNode { Id = "b", Type = "server", Bounds = new DiagramRect(400, 40, 100, 50) });
        diagram.Add(new DiagramNode { Id = "n", Type = "note", Bounds = new DiagramRect(0, 300, 100, 50) });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "arrow", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        diagram.ClearHistory();
        return diagram;
    }
}
