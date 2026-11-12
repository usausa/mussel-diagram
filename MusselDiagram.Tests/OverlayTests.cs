namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed class OverlayTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes = [new DiagramNodeType { Name = "server", GetTooltip = static x => x.Id == "b" ? "Server B" : null }],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        IconSets = [new DiagramIconSet { Prefix = "test", Icons = [new DiagramIcon { Id = "bolt", Body = "<path d=\"M0 0h1\" />" }] }]
    };

    private static readonly DiagramBadgeKind[] Kinds =
    [
        new() { Name = "status", Corner = DiagramCorner.TopRight, Order = 2 },
        new() { Name = "count", Corner = DiagramCorner.TopRight, Order = 1, Clickable = true },
        new() { Name = "owner", Corner = DiagramCorner.BottomLeft }
    ];

    public OverlayTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void StatesOverridesAndIssuesChangeTheLookOfElements()
    {
        var overlays = new Dictionary<string, DiagramOverlay>
        {
            ["a"] = new() { States = ["running", "not valid!"], Style = new DiagramRenderOverride { Stroke = "#ff0000", Opacity = 0.5, Dash = DiagramDash.Solid } },
            ["ab"] = new() { States = ["hot"], Style = new DiagramRenderOverride { StrokeWidth = 3 } },
            ["missing"] = new() { States = ["running"] }
        };
        var cut = RenderView(CreateDiagram(), overlays, [Issue(DiagramIssueSeverity.Error, "a", "Broken"), Issue(DiagramIssueSeverity.Warning, "b", "Odd")]);

        var a = cut.Find("g.mussel-diagram-node[data-id='a']");
        Assert.Contains("mussel-diagram-state-running", a.ClassList);
        Assert.Contains("mussel-diagram-error", a.ClassList);
        Assert.DoesNotContain(a.ClassList, static x => x.Contains("valid", StringComparison.Ordinal));
        Assert.Contains("--mussel-diagram-override-stroke: #ff0000;", a.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-override-opacity: 0.5;", a.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-override-dash: none;", a.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("Alpha\nBroken", a.QuerySelector(":scope > title")!.TextContent);
        var b = cut.Find("g.mussel-diagram-node[data-id='b']");
        Assert.Contains("mussel-diagram-warning", b.ClassList);
        Assert.Equal("Server B\nOdd", b.QuerySelector(":scope > title")!.TextContent);
        var link = cut.Find("g.mussel-diagram-link[data-id='ab']");
        Assert.Contains("mussel-diagram-state-hot", link.ClassList);
        Assert.Contains("--mussel-diagram-override-stroke-width: 3;", link.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void BadgesAreOrderedByCornerAndKind()
    {
        var overlays = new Dictionary<string, DiagramOverlay>
        {
            ["a"] = new()
            {
                Badges =
                [
                    new DiagramBadge { Kind = "status", Text = "Running", Tone = DiagramBadgeTone.Info },
                    new DiagramBadge { Kind = "other", Tooltip = "Unregistered" },
                    new DiagramBadge { Kind = "owner", Text = "Sato" },
                    new DiagramBadge { Kind = "count", Text = "3", Icon = "test:bolt", Tooltip = "Three runs" }
                ]
            },
            ["ab"] = new() { Badges = [new DiagramBadge { Tone = DiagramBadgeTone.Success }] }
        };
        var cut = RenderView(CreateDiagram(), overlays, [Issue(DiagramIssueSeverity.Warning, "a", "One"), Issue(DiagramIssueSeverity.Error, "a", "Two")]);

        var set = cut.Find(".mussel-diagram-badges > g.mussel-diagram-badge-set[data-for='a']");
        Assert.Equal("translate(0,0)", set.GetAttribute("transform"));
        Assert.Equal("80", set.GetAttribute("data-w"));
        var badges = set.QuerySelectorAll(":scope > g.mussel-diagram-badge");
        Assert.Equal(["top-right", "top-right", "top-right", "top-right", "bottom-left"], badges.Select(static x => x.GetAttribute("data-corner")));
        Assert.Equal(["error", "default", "info", "default", "default"], badges.Select(static x => x.GetAttribute("data-tone")));
        Assert.Equal("2", badges[0].QuerySelector("text")!.TextContent);
        Assert.Equal("One\nTwo", badges[0].QuerySelector("title")!.TextContent);
        Assert.Equal("3", badges[1].QuerySelector("text")!.TextContent);
        Assert.True(badges[1].HasAttribute("data-clickable"));
        Assert.NotNull(badges[1].QuerySelector("use.mussel-diagram-badge-icon"));
        Assert.Equal("Running", badges[2].QuerySelector("text")!.TextContent);
        Assert.Equal("8", badges[3].QuerySelector("rect")!.GetAttribute("rx"));
        Assert.Equal("Sato", badges[4].QuerySelector("text")!.TextContent);

        var line = cut.Find(".mussel-diagram-badges > g.mussel-diagram-badge-set[data-for='ab']");
        Assert.True(line.HasAttribute("data-link"));
        Assert.Equal("translate(140,20)", line.GetAttribute("transform"));
    }

    [Fact]
    public void OnlyElementsWhoseOverlaysChangedAreDrawnAgain()
    {
        var cut = RenderView(CreateDiagram(), new Dictionary<string, DiagramOverlay> { ["a"] = new() { States = ["running"] }, ["b"] = new() { States = ["idle"] } }, null);
        var boxes = cut.FindComponents<DiagramBoxView>().ToDictionary(static x => x.Instance.Box.Id);
        var (a, b) = (boxes["a"].RenderCount, boxes["b"].RenderCount);

        cut.Render(parameters => parameters.Add(static p => p.Overlays, new Dictionary<string, DiagramOverlay> { ["a"] = new() { States = ["running"] }, ["b"] = new() { States = ["done"] } }));

        Assert.Equal(a, boxes["a"].RenderCount);
        Assert.Equal(b + 1, boxes["b"].RenderCount);
        Assert.Contains("mussel-diagram-state-done", cut.Find("g.mussel-diagram-node[data-id='b']").ClassList);
    }

    [Fact]
    public async Task OnlyClickableBadgesAreReported()
    {
        var clicked = new List<DiagramBadgeClickedEventArgs>();
        var overlays = new Dictionary<string, DiagramOverlay>
        {
            ["a"] = new() { Badges = [new DiagramBadge { Kind = "status", Text = "Running" }, new DiagramBadge { Kind = "count", Text = "3" }] }
        };
        var cut = RenderView(CreateDiagram(), overlays, null, parameters => parameters.Add(p => p.BadgeClicked, clicked.Add));

        await cut.InvokeAsync(() => cut.Instance.OnBadgeClickedAsync("a", 0));
        await cut.InvokeAsync(() => cut.Instance.OnBadgeClickedAsync("a", 1));
        await cut.InvokeAsync(() => cut.Instance.OnBadgeClickedAsync("a", 5));

        var only = Assert.Single(clicked);
        Assert.Equal("a", only.ElementId);
        Assert.Equal("3", only.Badge.Text);
        Assert.Empty(cut.Instance.SelectedIds);
    }

    [Fact]
    public async Task NextAndPreviousIssuesGoFromTopToBottomAndLeftToRight()
    {
        var diagram = CreateDiagram();
        diagram.Add(new DiagramNode { Id = "c", Type = "server", Bounds = new DiagramRect(0, 200, 80, 40) });
        var cut = RenderView(diagram, null, [Issue(DiagramIssueSeverity.Error, "c", "C"), Issue(DiagramIssueSeverity.Error, "b", "B"), Issue(DiagramIssueSeverity.Warning, "a", "A"), Issue(DiagramIssueSeverity.Error, "gone", "Gone")]);
        var commands = cut.Instance.Commands;

        Assert.True(commands[DiagramCommands.NextIssue].IsEnabled);
        Assert.Equal(["F8"], commands[DiagramCommands.NextIssue].Keys);
        Assert.Equal(["Shift+F8"], commands[DiagramCommands.PreviousIssue].Keys);
        var visited = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.NextIssue));
            visited.Add(cut.Instance.SelectedIds.Single());
        }

        await cut.InvokeAsync(() => commands.ExecuteAsync(DiagramCommands.PreviousIssue));
        visited.Add(cut.Instance.SelectedIds.Single());

        Assert.Equal(["a", "b", "c", "a", "c"], visited);

        cut.Render(static parameters => parameters.Add(static p => p.Issues, null));
        Assert.False(commands[DiagramCommands.NextIssue].IsEnabled);
    }

    private static DiagramIssue Issue(DiagramIssueSeverity severity, string id, string message) =>
        new(severity, DiagramIssueKind.App, message) { ElementIds = [id] };

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "a", Type = "server", Bounds = new DiagramRect(0, 0, 80, 40), Label = "Alpha" });
        diagram.Add(new DiagramNode { Id = "b", Type = "server", Bounds = new DiagramRect(200, 0, 80, 40), Label = "Beta" });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "arrow", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        return diagram;
    }

    private IRenderedComponent<MusselDiagramView> RenderView(
        Diagram diagram,
        IReadOnlyDictionary<string, DiagramOverlay>? overlays,
        IReadOnlyList<DiagramIssue>? issues,
        Action<ComponentParameterCollectionBuilder<MusselDiagramView>>? more = null) =>
        Render<MusselDiagramView>(parameters =>
        {
            parameters
                .Add(static p => p.Diagram, diagram)
                .Add(static p => p.Overlays, overlays)
                .Add(static p => p.Issues, issues)
                .Add(static p => p.BadgeKinds, Kinds);
            more?.Invoke(parameters);
        });
}
