namespace MusselDiagram;

using System.Text.Json;

using AngleSharp.Dom;

public sealed class LookViewTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "service",
                Look = new DiagramLook { Fill = "#ffeecc" },
                Slots = [new DiagramLabelSlot { Name = "name", Format = new DiagramTextFormat { Color = "#333333" } }]
            }
        ],
        LinkTypes = [new DiagramLinkType { Name = "flow", SourceMarker = DiagramMarker.Circle, Look = new DiagramLook { Stroke = "#57606a", Dash = DiagramDash.Dashed } }]
    };

    private readonly BunitJSModuleInterop module;

    public LookViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void LinkLooksComeAfterTheTypeAndDrawTheirOwnMarkers()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        var ab = cut.Find("g.mussel-diagram-link[data-id='ab']");
        var style = ab.GetAttribute("style")!;
        Assert.True(style.IndexOf("--mussel-diagram-link-stroke: #e53935;", StringComparison.Ordinal) > style.IndexOf("--mussel-diagram-link-stroke: #57606a;", StringComparison.Ordinal));
        Assert.Contains("--mussel-diagram-link-stroke-width: 3;", style, StringComparison.Ordinal);
        Assert.True(style.IndexOf("--mussel-diagram-link-dash: none;", StringComparison.Ordinal) > style.IndexOf("--mussel-diagram-link-dash: 6 4;", StringComparison.Ordinal));
        var line = ab.QuerySelector("path.mussel-diagram-link-line")!;
        var hit = ab.QuerySelector("path.mussel-diagram-link-hit")!;
        Assert.False(line.HasAttribute("marker-end"));
        Assert.EndsWith(hit.GetAttribute("d")!.Split('L')[^1], line.GetAttribute("d"), StringComparison.Ordinal);
        var start = Marker(cut, line.GetAttribute("marker-start"));
        Assert.Equal("--mussel-diagram-marker-color: #e53935", start.GetAttribute("style"));
        Assert.Equal("M0,5L5,0L10,5L5,10Z", start.QuerySelector("path")!.GetAttribute("d"));

        var plain = cut.Find("g.mussel-diagram-link[data-id='ba'] > path.mussel-diagram-link-line");
        Assert.Equal("--mussel-diagram-marker-color: #57606a", Marker(cut, plain.GetAttribute("marker-start")).GetAttribute("style"));
    }

    [Fact]
    public async Task ChangingALookPutsItsMarkerInTheDefsAtOnce()
    {
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));

        await cut.InvokeAsync(() => diagram.Replace((DiagramLink)diagram.Find("ba")! with { Look = new DiagramLinkLook { Stroke = "#1e88e5", TargetMarker = DiagramMarker.OpenArrow } }));

        var line = cut.Find("g.mussel-diagram-link[data-id='ba'] > path.mussel-diagram-link-line");
        var end = Marker(cut, line.GetAttribute("marker-end"));
        Assert.Contains("mussel-diagram-marker-open", end.ClassList);
        Assert.Equal("--mussel-diagram-marker-color: #1e88e5", end.GetAttribute("style"));
    }

    [Fact]
    public void BoxLooksComeAfterTheTypeAndOverridesStillWin()
    {
        var overlays = new Dictionary<string, DiagramOverlay> { ["a"] = new() { Style = new DiagramRenderOverride { Fill = "#ff0000" } } };
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(static p => p.Overlays, overlays));

        var a = cut.Find("g.mussel-diagram-node[data-id='a']");
        var style = a.GetAttribute("style")!;
        Assert.True(style.IndexOf("--mussel-diagram-shape-fill: #e3f2fd;", StringComparison.Ordinal) > style.IndexOf("--mussel-diagram-shape-fill: #ffeecc;", StringComparison.Ordinal));
        Assert.Contains("--mussel-diagram-shape-stroke: #1e88e5;", style, StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-shape-stroke-width: 2;", style, StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-shape-dash: 2 3;", style, StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-text-color: #0d47a1;", style, StringComparison.Ordinal);
        Assert.Contains("--mussel-diagram-override-fill: #ff0000;", style, StringComparison.Ordinal);
        Assert.False(a.QuerySelector("text.mussel-diagram-label")!.HasAttribute("style"));
        Assert.Equal("fill: #333333;", cut.Find("g.mussel-diagram-node[data-id='b'] text.mussel-diagram-label").GetAttribute("style"));
    }

    [Fact]
    public void TheScriptGetsTheFormAndEndsOfEachLink()
    {
        Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        var links = ((JsonElement)module.Invocations["attach"][0].Arguments[3]!).GetProperty("links").EnumerateArray().ToDictionary(static x => x.GetProperty("id").GetString()!);
        Assert.Equal(("straight", 0d), (links["ab"].GetProperty("form").GetString(), links["ab"].GetProperty("targetCut").GetDouble()));
        Assert.Equal("orthogonal", links["ba"].GetProperty("form").GetString());
        Assert.True(links["ba"].GetProperty("targetCut").GetDouble() > 0);
    }

    private static IElement Marker(IRenderedComponent<MusselDiagramView> cut, string? reference)
    {
        Assert.NotNull(reference);
        return cut.Find($"defs > marker#{reference["url(#".Length..^1]}");
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode
        {
            Id = "a",
            Type = "service",
            Bounds = new DiagramRect(0, 0, 80, 40),
            Label = "A",
            Look = new DiagramBoxLook { Fill = "#e3f2fd", Stroke = "#1e88e5", StrokeWidth = 2, Dash = DiagramDash.Dotted, TextColor = "#0d47a1" }
        });
        diagram.Add(new DiagramNode { Id = "b", Type = "service", Bounds = new DiagramRect(300, 0, 80, 40), Label = "B" });
        diagram.Connect(new DiagramLink
        {
            Id = "ab",
            Type = "flow",
            Source = new DiagramLinkEnd("a"),
            Target = new DiagramLinkEnd("b"),
            Look = new DiagramLinkLook { Stroke = "#e53935", StrokeWidth = 3, Dash = DiagramDash.Solid, SourceMarker = DiagramMarker.Diamond, TargetMarker = DiagramMarker.None, Form = DiagramLineForm.Straight }
        });
        diagram.Connect(new DiagramLink { Id = "ba", Type = "flow", Source = new DiagramLinkEnd("b"), Target = new DiagramLinkEnd("a") });
        return diagram;
    }
}
