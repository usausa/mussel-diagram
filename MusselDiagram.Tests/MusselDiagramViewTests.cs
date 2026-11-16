namespace MusselDiagram;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed class MusselDiagramViewTests : BunitContext
{
    private const string EmptyExport = "{\"missing\":[],\"empty\":true}";

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

    [Fact]
    public async Task AnExportKeepsEveryElementUntilTheOtherExportEnds()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var svg = controller.Setup<string>("exportDiagram", static x => FormatOf(x) == "svg");
        var png = controller.Setup<string>("exportDiagram", static x => FormatOf(x) == "png");
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, -1000, 0, 200, 200));
        Task<DiagramExportResult>[] exports = [];

        await cut.InvokeAsync(() =>
        {
            exports = [cut.Instance.ExportSvgAsync(), cut.Instance.ExportImageAsync()];
        });
        await cut.InvokeAsync(() => svg.SetResult(EmptyExport));
        await exports[0].WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(2, cut.FindAll("g.mussel-diagram-node").Count);

        await cut.InvokeAsync(() => png.SetResult(EmptyExport));
        await exports[1].WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Empty(cut.FindAll("g.mussel-diagram-node"));
    }

    [Fact]
    public async Task CallsAfterTheCircuitIsGoneDoNotThrow()
    {
        var controller = module.SetupModule("attach", static _ => true);
        controller.Setup<string>("exportDiagram", static _ => true).SetException(new JSDisconnectedException("gone"));
        controller.SetupVoid("setView", static _ => true).SetException(new JSDisconnectedException("gone"));
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        Task<DiagramExportResult>? export = null;

        await cut.InvokeAsync(() =>
        {
            export = cut.Instance.ExportSvgAsync();
        });
        await cut.InvokeAsync(() => cut.Instance.SetViewportAsync(1, new DiagramPoint(0, 0)).AsTask());
        var result = await export!.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Null(result.Content);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task DisposingWhileTheScriptIsAttachingEndsQuietly()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var setCommands = controller.SetupVoid("setCommands", static _ => true);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(setCommands.SetVoidResult);
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.NotSame(Renderer.UnhandledException, ended);
    }

    [Fact]
    public async Task DisposingWhileTheCommandsAreSentEndsQuietly()
    {
        var runtime = new LateRuntime { Module = { Hold = "setCommands" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var diagram = new Diagram(Schema with { Rules = new DiagramAppRules { InPalette = static _ => true } });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));

        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.NotSame(Renderer.UnhandledException, ended);
    }

    [Fact]
    public async Task DisposingWhileTheOptionsAreSentEndsQuietly()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var setOptions = controller.SetupVoid("setOptions", static _ => true);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        cut.Render(static p => p.Add(static x => x.TooltipEnabled, false));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(setOptions.SetVoidResult);
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.NotSame(Renderer.UnhandledException, ended);
    }

    [Fact]
    public async Task TheScriptThatArrivesAfterTheViewIsGoneIsReleased()
    {
        var runtime = new LateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.WaitForAssertionAsync(() => Assert.True(runtime.Module.Released));
        Assert.DoesNotContain("attach", runtime.Module.Calls);
    }

    [Fact]
    public async Task CallsBeforeTheScriptIsAttachedAreAppliedInOrder()
    {
        var runtime = new LateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());
        await cut.InvokeAsync(() => cut.Instance.ZoomToFitAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.CenterOnAsync("b").AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", runtime.Module.Calls));
        Assert.Equal(["select", "fit", "centerOn", "ready"], runtime.Module.Calls.Where(static x => x is "select" or "fit" or "centerOn" or "ready"));
    }

    [Fact]
    public async Task CallsWhileTheScriptIsAttachingKeepTheirOrder()
    {
        var runtime = new LateRuntime { Module = { Hold = "select" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());
        await cut.InvokeAsync(() => cut.Instance.ZoomToFitAsync().AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("select", runtime.Module.Calls));
        await cut.InvokeAsync(() => cut.Instance.CenterOnAsync("b").AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", runtime.Module.Calls));
        Assert.Equal(["select", "fit", "centerOn", "ready"], runtime.Module.Calls.Where(static x => x is "select" or "fit" or "centerOn" or "ready"));
    }

    [Fact]
    public async Task SourcesReleaseTheScriptThatArrivesAfterTheyAreGone()
    {
        var runtime = new LateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));
        var source = Render<MusselDiagramDragSource>(parameters => parameters.Add(p => p.View, view.Instance).Add(static p => p.Type, "service"));

        await palette.InvokeAsync(() => palette.Instance.DisposeAsync().AsTask());
        await source.InvokeAsync(() => source.Instance.DisposeAsync().AsTask());
        await view.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await view.WaitForAssertionAsync(() => Assert.True(runtime.Module.Released));
        Assert.DoesNotContain("attachSource", runtime.Module.Calls);
    }

    [Fact]
    public async Task AnExceptionInAHandlerCalledFromTheScriptReachesTheRenderer()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(static p => p.SelectionChanged, static _ => throw new InvalidOperationException("app")));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => bridge.OnSelectionChanged(["a"])));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public async Task AnExceptionInTheViewportHandlerReachesTheRenderer()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(static p => p.ViewportChanged, static _ => throw new InvalidOperationException("app")));
        var bridge = new DiagramScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.OnViewportChanged(1, 0, 0, 200, 200));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.IsType<InvalidOperationException>(await Renderer.UnhandledException.ConfigureAwait(true));
    }

    private static string? FormatOf(JSRuntimeInvocation invocation) =>
        invocation.Arguments is [JsonElement options, ..] ? options.GetProperty("format").GetString() : null;

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
