namespace MusselDiagram;

using System.Text.Json;

public sealed class TemplateTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "card",
                Size = new DiagramSize(120, 60),
                Look = new DiagramLook { Shape = DiagramShape.Ellipse },
                Slots = [new DiagramLabelSlot { Name = "title" }, new DiagramLabelSlot { Name = "body", DefaultText = "Details" }],
                Ports = [new DiagramPortDefinition { Id = "out", Side = DiagramSide.Right }]
            },
            new DiagramNodeType { Name = "service", Look = new DiagramLook { Shape = DiagramShape.Ellipse } }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }]
    };

    private readonly BunitJSModuleInterop module;

    private int drawn;

    public TemplateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void TemplatesDrawTheirTypesAndThePortsStay()
    {
        var cut = RenderView(CreateDiagram());

        var card = cut.Find("g.mussel-diagram-node[data-id='c']");
        Assert.True(card.HasAttribute("data-template"));
        Assert.Equal("120", card.QuerySelector("rect.card-frame")!.GetAttribute("width"));
        Assert.Equal("Order", card.QuerySelector("text[data-slot='title']")!.TextContent);
        Assert.Null(card.QuerySelector(".mussel-diagram-shape"));
        Assert.Null(card.QuerySelector(".mussel-diagram-label"));
        Assert.NotNull(card.QuerySelector("circle.mussel-diagram-port[data-port='out']"));
        var service = cut.Find("g.mussel-diagram-node[data-id='s']");
        Assert.False(service.HasAttribute("data-template"));
        Assert.NotNull(service.QuerySelector(".mussel-diagram-shape"));

        var table = (JsonElement)module.Invocations["attach"][0].Arguments[3]!;
        var boxes = table.GetProperty("boxes").EnumerateArray().ToDictionary(static x => x.GetProperty("id").GetString()!);
        Assert.Equal("Order", boxes["c"].GetProperty("texts").GetProperty("title").GetString());
        Assert.Equal(string.Empty, boxes["c"].GetProperty("texts").GetProperty("body").GetString());
        Assert.False(boxes["s"].TryGetProperty("texts", out _));
        var schema = (JsonElement)module.Invocations["attach"][0].Arguments[4]!;
        var types = schema.GetProperty("types").EnumerateArray().ToDictionary(static x => x.GetProperty("name").GetString()!);
        Assert.Equal("rectangle", types["card"].GetProperty("shape").GetString());
        Assert.Equal("ellipse", types["service"].GetProperty("shape").GetString());
    }

    [Fact]
    public async Task TemplatesAreDrawnAgainOnlyWhenTheirNodeChanges()
    {
        var diagram = CreateDiagram();
        var cut = RenderView(diagram);
        var count = drawn;

        await cut.InvokeAsync(() => diagram.Move(["s"], new DiagramPoint(10, 0)));
        cut.Render(static p => p.Add(static x => x.GridVisible, true));

        Assert.Equal(count, drawn);

        await cut.InvokeAsync(() => diagram.Replace((DiagramNode)diagram.Find("c")! with { Label = "Invoice" }));

        Assert.Equal(count + 1, drawn);
        Assert.Equal("Invoice", cut.Find("g.mussel-diagram-node[data-id='c'] text[data-slot='title']").TextContent);
    }

    [Fact]
    public void RemovingTheTemplateDrawsTheTypeAgain()
    {
        var cut = RenderView(CreateDiagram());

        cut.Render(static p => p.AddChildContent(string.Empty));

        var card = cut.Find("g.mussel-diagram-node[data-id='c']");
        Assert.False(card.HasAttribute("data-template"));
        Assert.NotNull(card.QuerySelector(".mussel-diagram-shape"));
        Assert.Equal("Order", card.QuerySelector("text.mussel-diagram-label")!.GetAttribute("data-label"));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "c", Type = "card", Bounds = new DiagramRect(0, 0, 120, 60), Label = "Order" });
        diagram.Add(new DiagramNode { Id = "s", Type = "service", Bounds = new DiagramRect(200, 0, 80, 40), Label = "Web" });
        return diagram;
    }

    private IRenderedComponent<MusselDiagramView> RenderView(Diagram diagram) =>
        Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .AddChildContent<DiagramTemplate>(template => template
                .Add(static p => p.Type, "card")
                .Add(static p => p.ChildContent, context =>
                {
                    drawn++;
                    return $"<rect class=\"card-frame\" width=\"{context.Width}\" height=\"{context.Height}\"></rect><text data-slot=\"title\">{context.Label}</text>";
                })));
}
