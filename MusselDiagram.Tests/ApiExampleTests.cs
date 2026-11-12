namespace MusselDiagram;

public sealed class ApiExampleTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "server",
                DisplayName = "Server",
                Ports = [new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }]
            },
            new DiagramNodeType
            {
                Name = "database",
                DisplayName = "Database",
                Look = new DiagramLook { Shape = DiagramShape.Ellipse },
                Ports = [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left }]
            }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }],
        DefaultLinkType = "arrow"
    };

    public ApiExampleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public async Task QuickStartWorks()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramNode { Id = "web", Type = "server", Bounds = new DiagramRect(0, 0, 120, 60) });
        diagram.Add(new DiagramNode { Id = "db", Type = "database", Bounds = new DiagramRect(300, 0, 120, 60) });
        var connected = new List<string>();
        var view = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.Connected, e => connected.Add($"{e.Source.ElementId} -> {e.Target.ElementId}")));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

        var result = await view.InvokeAsync(() => view.Instance.CommitConnectAsync(new DiagramLinkEnd("web", "out"), new DiagramLinkEnd("db", "in"), "arrow", diagram.Snapshot.Version, 0));

        Assert.True(result.Accepted);
        Assert.Equal(["web -> db"], connected);
        Assert.Equal(["Server", "Database"], palette.FindAll(".mussel-diagram-palette-item").Select(static x => x.TextContent.Trim()));
        Assert.Equal("ellipse", palette.Find(".mussel-diagram-palette-item[data-mussel-type='database'] .mussel-diagram-palette-shape").LocalName);
        Assert.Empty(diagram.Validate());
        var copy = new Diagram(Schema);
        copy.LoadJson(diagram.ToJson());
        Assert.Equal(new DiagramLinkEnd("db", "in"), Assert.Single(copy.Snapshot.Links).Target);
    }
}
