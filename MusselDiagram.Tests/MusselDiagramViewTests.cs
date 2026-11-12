namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed class MusselDiagramViewTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "service",
                Look = new DiagramLook { Shape = DiagramShape.Ellipse, Fill = "#ffeecc", Icon = "test:gear" },
                Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, MaxLines = 2 }],
                Ports = [new DiagramPortDefinition { Id = "in", Side = DiagramSide.Left, Label = "Input" }, new DiagramPortDefinition { Id = "out", Side = DiagramSide.Right }]
            }
        ],
        GroupTypes = [new DiagramGroupType { Name = "vpc", Look = new DiagramLook { Dash = DiagramDash.Dashed } }],
        LinkTypes = [new DiagramLinkType { Name = "flow", SourceMarker = DiagramMarker.Circle }],
        IconSets = [new DiagramIconSet { Prefix = "test", Icons = [new DiagramIcon { Id = "gear", Body = "<circle cx=\"12\" cy=\"12\" r=\"8\" />" }] }]
    };

    private readonly BunitJSModuleInterop module;

    public MusselDiagramViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void ViewDrawsTheLayersAndAttachesTheScript()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .AddUnmatched("class", "app-view"));

        Assert.Contains("app-view", cut.Find(".mussel-diagram").ClassList);
        Assert.Equal("4", cut.Find(".mussel-diagram").GetAttribute("data-version"));
        Assert.Single(cut.FindAll(".mussel-diagram-stage > svg.mussel-diagram-overlay"));
        Assert.Single(cut.FindAll(".mussel-diagram-groups > g.mussel-diagram-group[data-id='vpc']"));
        Assert.Equal(2, cut.FindAll(".mussel-diagram-nodes > g.mussel-diagram-node").Count);
        Assert.Single(cut.FindAll(".mussel-diagram-links > g.mussel-diagram-link[data-id='ab']"));
        module.VerifyInvoke("attach");
    }

    [Fact]
    public void BoxesCarryTheirShapeIconLabelAndPorts()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        var node = cut.Find("g.mussel-diagram-node[data-id='a']");
        Assert.Equal("service", node.GetAttribute("data-type"));
        Assert.Equal("translate(40,60)", node.GetAttribute("transform"));
        Assert.Contains("--mussel-diagram-shape-fill: #ffeecc;", node.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("40", node.QuerySelector("ellipse")!.GetAttribute("rx"));
        var symbolId = cut.Find("defs > symbol").GetAttribute("id");
        Assert.Equal($"#{symbolId}", node.QuerySelector("use.mussel-diagram-icon")!.GetAttribute("href"));
        var label = node.QuerySelector("text.mussel-diagram-label")!;
        Assert.Equal("Web server", label.GetAttribute("data-label"));
        Assert.Equal("2", label.GetAttribute("data-lines"));
        Assert.Equal("top", label.GetAttribute("data-valign"));
        Assert.Equal("44", label.GetAttribute("y"));
        Assert.Empty(label.ChildNodes);
        Assert.Equal(["in", "out"], node.QuerySelectorAll("circle.mussel-diagram-port").Select(static x => x.GetAttribute("data-port")));
        Assert.Equal(["Input", null], node.QuerySelectorAll("circle.mussel-diagram-port").Select(static x => x.QuerySelector("title")?.TextContent));

        var group = cut.Find("g.mussel-diagram-group[data-id='vpc']");
        Assert.Contains("--mussel-diagram-shape-dash: 6 4;", group.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("Production", group.QuerySelector("text")!.GetAttribute("data-label"));
    }

    [Fact]
    public void LinksAndUnregisteredTypesAreDrawn()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        var line = cut.Find("g.mussel-diagram-link[data-id='ab'] > path.mussel-diagram-link-line");
        Assert.Equal("M130,80L290,80", line.GetAttribute("d"));
        Assert.Equal("M120,80L300,80", cut.Find("g.mussel-diagram-link[data-id='ab'] > path.mussel-diagram-link-hit").GetAttribute("d"));
        Assert.StartsWith("url(#mussel-diagram-", line.GetAttribute("marker-start"), StringComparison.Ordinal);
        Assert.StartsWith("url(#mussel-diagram-", line.GetAttribute("marker-end"), StringComparison.Ordinal);

        var unknown = cut.Find("g.mussel-diagram-node[data-id='b']");
        Assert.Contains("mussel-diagram-unregistered", unknown.ClassList);
        Assert.Equal("legacy", unknown.QuerySelector("text")!.GetAttribute("data-label"));
    }

    [Fact]
    public async Task OnlyChangedElementsAreDrawnAgain()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var boxes = cut.FindComponents<DiagramBoxView>().ToDictionary(static x => x.Instance.Box.Id);
        var links = cut.FindComponents<DiagramLinkView>().ToDictionary(static x => x.Instance.Link.Id);
        var (a, b, vpc, ab) = (boxes["a"].RenderCount, boxes["b"].RenderCount, boxes["vpc"].RenderCount, links["ab"].RenderCount);

        await cut.InvokeAsync(() => diagram.Move(["a"], new DiagramPoint(10, 0)));

        Assert.Equal(a + 1, boxes["a"].RenderCount);
        Assert.Equal(b, boxes["b"].RenderCount);
        Assert.Equal(vpc, boxes["vpc"].RenderCount);
        Assert.Equal(ab + 1, links["ab"].RenderCount);
        Assert.Equal("translate(50,60)", cut.Find("g.mussel-diagram-node[data-id='a']").GetAttribute("transform"));
        Assert.Equal("5", cut.Find(".mussel-diagram").GetAttribute("data-version"));
    }

    [Fact]
    public async Task ElementsFarOutsideTheViewAreNotDrawn()
    {
        DiagramViewport? reported = null;
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(p => p.ViewportChanged, viewport => reported = viewport));

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, -1000, 0, 200, 200));

        Assert.Empty(cut.FindAll("g.mussel-diagram-node"));
        Assert.Empty(cut.FindAll("g.mussel-diagram-group"));
        Assert.Empty(cut.FindAll("g.mussel-diagram-link"));
        Assert.Equal(new DiagramRect(1000, 0, 200, 200), reported?.VisibleBounds);
        Assert.Equal(new DiagramPoint(1100, 100), cut.Instance.Center);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(2, 0, 0, 200, 200));

        Assert.Equal(["a"], cut.FindAll("g.mussel-diagram-node").Select(static x => x.GetAttribute("data-id")));
        Assert.Single(cut.FindAll("g.mussel-diagram-group"));
        Assert.Single(cut.FindAll("g.mussel-diagram-link"));
        Assert.Equal(2, cut.Instance.Zoom);
    }

    [Fact]
    public async Task LoadingSendsTheWholeTableAgain()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var diagram = CreateDiagram();
        var other = new Diagram(Schema);
        other.Add(new DiagramNode { Id = "c", Type = "service", Bounds = new DiagramRect(0, 0, 80, 40) });
        var json = other.ToJson();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => cut.Instance.OnSelectionChangedAsync(["a", "b"]));

        await cut.InvokeAsync(() => diagram.LoadJson(json));

        Assert.Equal(["c"], cut.FindAll("g.mussel-diagram-node").Select(static x => x.GetAttribute("data-id")));
        Assert.Empty(cut.Instance.SelectedIds);
        controller.VerifyInvoke("setTable");
        Assert.Empty(controller.Invocations["applyChanges"]);
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 600, 400), Label = "Production" });
        diagram.Add(new DiagramNode { Id = "a", Type = "service", Bounds = new DiagramRect(40, 60, 80, 40), ParentId = "vpc", Label = "Web server" });
        diagram.Add(new DiagramNode { Id = "b", Type = "legacy", Bounds = new DiagramRect(300, 60, 80, 40) });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "flow", Source = new DiagramLinkEnd("a", "out"), Target = new DiagramLinkEnd("b") });
        return diagram;
    }
}
