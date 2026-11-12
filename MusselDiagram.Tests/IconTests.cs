namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed class IconTests : BunitContext
{
    private const string LazySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 48 32\"><script>alert(1)</script><rect width=\"48\" height=\"32\"/></svg>";

    private readonly BunitJSModuleInterop module;

    public IconTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public async Task OnlyTheIconsInUseAreDefined()
    {
        var diagram = new Diagram(CreateSchema(Inline("gear"), Inline("bolt"), Inline("star")));
        diagram.Add(new DiagramNode { Id = "a", Type = "service", Bounds = new DiagramRect(0, 0, 80, 40) });
        diagram.Add(new DiagramNode { Id = "b", Type = "plain", Bounds = new DiagramRect(200, 0, 80, 40), Icon = "test:star" });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));

        Assert.Equal([IconOf(cut, "a"), IconOf(cut, "b")], Symbols(cut));

        await cut.InvokeAsync(() => diagram.Add(new DiagramNode { Id = "c", Type = "worker", Bounds = new DiagramRect(400, 0, 80, 40) }));

        Assert.Equal([IconOf(cut, "a"), IconOf(cut, "b"), IconOf(cut, "c")], Symbols(cut));
    }

    [Fact]
    public async Task IconsAreReadWhenTheyAreFirstUsed()
    {
        var reads = new List<string>();
        var read = new TaskCompletionSource<string?>();
        var diagram = new Diagram(CreateSchema(Lazy("lazy", reads, read.Task), Lazy("unused", reads, read.Task)));
        diagram.Add(new DiagramNode { Id = "a", Type = "plain", Bounds = new DiagramRect(0, 0, 80, 40), Icon = "test:lazy" });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));

        Assert.Empty(Symbols(cut));
        Assert.Equal(["lazy"], reads);

        await cut.InvokeAsync(() => read.SetResult(LazySvg));

        await cut.WaitForAssertionAsync(() => Assert.Equal([IconOf(cut, "a")], Symbols(cut)));
        var symbol = cut.Find("defs > symbol");
        Assert.Equal("0 0 48 32", symbol.GetAttribute("viewBox"));
        Assert.Empty(symbol.QuerySelectorAll("script"));
        Assert.Single(symbol.QuerySelectorAll("rect"));
        Assert.Equal(["lazy"], reads);
    }

    [Fact]
    public void PaletteRowsAskForTheirIcons()
    {
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(CreateSchema(Inline("gear"), Inline("bolt"), Inline("star")))));
        Assert.Empty(Symbols(view));

        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

        var rows = palette.FindAll(".mussel-diagram-palette-item use").Select(static x => x.GetAttribute("href")![1..]).ToList();
        Assert.Equal(2, rows.Count);
        view.WaitForAssertion(() => Assert.Equal(rows.Order(), Symbols(view).Order()));
    }

    [Fact]
    public async Task ThePickerAsksForTheIconsItShows()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(CreateSchema(Inline("gear"), Inline("bolt"), Inline("star")))));
        var id = cut.Instance.RenderContext.FindIconId("test:bolt")!;

        await cut.InvokeAsync(() => new DiagramScriptBridge(cut.Instance).WantIcons([id, "unknown"]));

        Assert.Equal([id], Symbols(cut));
    }

    [Fact]
    public void BadgeIconsAreDefined()
    {
        var diagram = new Diagram(CreateSchema(Inline("gear"), Inline("bolt"), Inline("star")));
        diagram.Add(new DiagramNode { Id = "a", Type = "plain", Bounds = new DiagramRect(0, 0, 80, 40) });
        var overlays = new Dictionary<string, DiagramOverlay> { ["a"] = new() { Badges = [new DiagramBadge { Icon = "test:star", Text = "1" }] } };

        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram).Add(static p => p.Overlays, overlays));

        Assert.Equal([cut.Find("use.mussel-diagram-badge-icon").GetAttribute("href")![1..]], Symbols(cut));
    }

    [Fact]
    public async Task ExportWaitsForTheIconsInUse()
    {
        var controller = module.SetupModule("attach", static _ => true);
        controller.Setup<string>("exportDiagram", static _ => true).SetResult("{\"missing\":[],\"empty\":true}");
        var reads = new List<string>();
        var read = new TaskCompletionSource<string?>();
        var diagram = new Diagram(CreateSchema(Lazy("lazy", reads, read.Task)));
        diagram.Add(new DiagramNode { Id = "a", Type = "plain", Bounds = new DiagramRect(0, 0, 80, 40), Icon = "test:lazy" });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        Task<DiagramExportResult>? export = null;

        await cut.InvokeAsync(() =>
        {
            export = cut.Instance.ExportSvgAsync();
        });

        Assert.Empty(controller.Invocations["exportDiagram"]);

        await cut.InvokeAsync(() => read.SetResult(LazySvg));
        await export!.ConfigureAwait(true);

        controller.VerifyInvoke("exportDiagram");
        Assert.Equal([IconOf(cut, "a")], Symbols(cut));
    }

    [Fact]
    public async Task NothingIsReadAfterTheViewIsGone()
    {
        var reads = new List<string>();
        var read = new TaskCompletionSource<string?>();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(CreateSchema(Lazy("lazy", reads, read.Task)))));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.WantIcon("test:lazy"));

        Assert.Empty(reads);
    }

    private static DiagramSchema CreateSchema(params DiagramIcon[] icons) => new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "service", Look = new DiagramLook { Icon = "test:gear" } },
            new DiagramNodeType { Name = "worker", Look = new DiagramLook { Icon = "test:bolt" } },
            new DiagramNodeType { Name = "plain" }
        ],
        IconSets = [new DiagramIconSet { Prefix = "test", Icons = [.. icons] }]
    };

    private static DiagramIcon Inline(string id) => new() { Id = id, Body = "<circle cx=\"12\" cy=\"12\" r=\"8\"/>" };

    private static DiagramIcon Lazy(string id, List<string> reads, Task<string?> read) =>
        new()
        {
            Id = id,
            Load = _ =>
            {
                reads.Add(id);
                return read;
            }
        };

    private static List<string> Symbols(IRenderedComponent<MusselDiagramView> cut) =>
        [.. cut.FindAll("defs > symbol").Select(static x => x.GetAttribute("id")!)];

    private static string IconOf(IRenderedComponent<MusselDiagramView> cut, string id) =>
        cut.Find($"g.mussel-diagram-node[data-id='{id}'] use.mussel-diagram-icon").GetAttribute("href")![1..];
}
