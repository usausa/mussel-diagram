namespace MusselDiagram;

using System.Text.Json;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
    public async Task PansInsideTheDrawnAreaDoNotDrawAgain()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, 0, 0, 200, 200));
        var renders = cut.RenderCount;

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, -50, -50, 200, 200));

        Assert.Equal(renders, cut.RenderCount);
        Assert.Equal(new DiagramPoint(150, 150), cut.Instance.Center);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(1, -500, 0, 200, 200));

        Assert.True(cut.RenderCount > renders);
    }

    [Fact]
    public async Task AnAppThatDrawsOnViewportChangesDrawsTheViewOnce()
    {
        var plain = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var host = Render<ViewportHost>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var view = host.FindComponent<MusselDiagramView>();
        var (alone, hosted) = (plain.RenderCount, view.RenderCount);

        await plain.InvokeAsync(() => plain.Instance.OnViewportChanged(2, 0, 0, 200, 200));
        await host.InvokeAsync(() => view.Instance.OnViewportChanged(2, 0, 0, 200, 200));

        Assert.Equal(plain.RenderCount - alone, view.RenderCount - hosted);
        Assert.Equal(2, host.Instance.Zoom);
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
    public async Task AnotherDiagramTellsTheScriptToDropWhatItShowsAhead()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var diagram = CreateDiagram();
        var other = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => diagram.LoadJson(other.ToJson()));
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(controller.Invocations["setTable"]));
        var loaded = controller.Invocations["setTable"].Count;

        cut.Render(parameters => parameters.Add(static p => p.Diagram, other));

        await cut.WaitForAssertionAsync(() => Assert.True(controller.Invocations["setTable"].Count > loaded));
        Assert.Equal((false, true), ((bool)controller.Invocations["setTable"][loaded - 1].Arguments[1]!, (bool)controller.Invocations["setTable"][^1].Arguments[1]!));
    }

    [Fact]
    public async Task APaletteBeforeTheViewFollowsAnotherDiagram()
    {
        var other = new Diagram(new DiagramSchema { NodeTypes = [new DiagramNodeType { Name = "printer" }] });
        var host = Render<PaletteFirstHost>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await host.WaitForAssertionAsync(() => Assert.Contains("service", host.FindAll(".mussel-diagram-palette-item").Select(static x => x.GetAttribute("data-mussel-type"))));

        host.Render(parameters => parameters.Add(static p => p.Diagram, other));

        Assert.Equal(["printer"], host.FindAll(".mussel-diagram-palette-item").Select(static x => x.GetAttribute("data-mussel-type")));
    }

    [Fact]
    public async Task ChangesThatWaitForTheScriptAreSentInOneCall()
    {
        var runtime = new LateRuntime { Module = { Hold = "attach" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.InvokeAsync(() =>
        {
            diagram.Move(["b"], new DiagramPoint(10, 0));
            diagram.Add(new DiagramNode { Id = "c", Type = "service", Bounds = new DiagramRect(300, 200, 80, 40) });
            diagram.Delete(["c"]);
            diagram.Add(new DiagramNode { Id = "c", Type = "service", Bounds = new DiagramRect(320, 200, 80, 40) });
            diagram.Add(new DiagramNode { Id = "d", Type = "service", Bounds = new DiagramRect(300, 300, 80, 40) });
        });

        await cut.InvokeAsync(runtime.Module.Resume.SetResult);
        cut.Render();

        var sent = Assert.Single(runtime.Module.Calls.Select(static (name, index) => (name, index)), static x => x.name == "applyChanges");
        var changes = (JsonElement)runtime.Module.Arguments[sent.index]![0]!;
        Assert.Equal(cut.Instance.ScriptVersion, changes.GetProperty("version").GetInt64());
        Assert.Equal(["b", "c", "d"], changes.GetProperty("boxes").EnumerateArray().Select(static x => x.GetProperty("id").GetString()));
        Assert.Equal(320, changes.GetProperty("boxes")[1].GetProperty("x").GetDouble());
        Assert.Equal(["c"], changes.GetProperty("removed").EnumerateArray().Select(static x => x.GetString()));
    }

    [Fact]
    public async Task ChangesWhileTheScriptIsAttachedAreSentAfterIt()
    {
        var runtime = new LateRuntime { Module = { Hold = "attach" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var diagram = CreateDiagram();
        var other = new Diagram(Schema);
        other.Add(new DiagramNode { Id = "c", Type = "service", Bounds = new DiagramRect(0, 0, 80, 40) });
        var json = other.ToJson();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.InvokeAsync(() => diagram.LoadJson(json));
        cut.Render(static parameters => parameters.Add(static p => p.ReadOnly, true));
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        var after = runtime.Module.Calls.SkipWhile(static x => x != "ready").ToList();
        Assert.Contains("setOptions", after);
        Assert.Contains("setTable", after);
    }

    [Fact]
    public async Task ChangesOfTheOrderSendTheWholeTableAgain()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        var tables = controller.Invocations["setTable"].Count;

        await cut.InvokeAsync(() => diagram.Move(["b"], new DiagramPoint(10, 0)));
        await cut.InvokeAsync(() => diagram.Add(new DiagramNode { Id = "c", Type = "service", Bounds = new DiagramRect(300, 200, 80, 40) }));
        var appended = controller.Invocations["setTable"].Count;
        await cut.InvokeAsync(() => diagram.SendToBack(["c"]));
        var reordered = controller.Invocations["setTable"].Count;
        await cut.InvokeAsync(() => diagram.Delete(["a"]));
        await cut.InvokeAsync(diagram.Undo);
        var restored = controller.Invocations["setTable"].Count;

        Assert.Equal(tables, appended);
        Assert.Equal(appended + 1, reordered);
        Assert.Equal(reordered + 1, restored);
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
    public async Task ImagesThatCouldNotBeEmbeddedComeWithAMessage()
    {
        var controller = module.SetupModule("attach", static _ => true);
        controller.Setup<string>("exportDiagram", static _ => true).SetResult("""{"missing":[{"url":"https://example.com/a.png","ids":["a"]}],"empty":true}""");
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        Task<DiagramExportResult>? export = null;

        await cut.InvokeAsync(() =>
        {
            export = cut.Instance.ExportSvgAsync();
        });
        var result = await export!.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("The image 'https://example.com/a.png' could not be embedded.", issue.Message);
    }

    [Fact]
    public async Task AnExportWaitsForTheRoutesStillBeingComputed()
    {
        var controller = module.SetupModule("attach", static _ => true);
        controller.Setup<string>("exportDiagram", static _ => true).SetResult(EmptyExport);
        var router = new HeldRouter();
        var diagram = new Diagram(Schema with { Router = router });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() =>
        {
            diagram.Add(new DiagramNode { Id = "a", Type = "service", Bounds = new DiagramRect(0, 0, 80, 40) });
            diagram.Add(new DiagramNode { Id = "b", Type = "service", Bounds = new DiagramRect(300, 0, 80, 40) });
            diagram.Connect(new DiagramLink { Id = "ab", Type = "flow", Source = new DiagramLinkEnd("a", "out"), Target = new DiagramLinkEnd("b", "in") });
        });
        Task<DiagramExportResult>? export = null;

        await cut.InvokeAsync(() =>
        {
            export = cut.Instance.ExportSvgAsync();
        });
        await Task.Delay(300, Xunit.TestContext.Current.CancellationToken);
        var early = controller.Invocations["exportDiagram"].Count;
        await cut.InvokeAsync(router.Answer);
        await export!.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(0, early);
        Assert.Single(controller.Invocations["exportDiagram"]);
        Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("ab")!).RouteState);
    }

    [Fact]
    public async Task RoutesFromTheRouterAloneAreNotSentToTheScript()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var router = new HeldRouter();
        var diagram = new Diagram(Schema with { Router = router });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() =>
        {
            diagram.Add(new DiagramNode { Id = "a", Type = "service", Bounds = new DiagramRect(0, 0, 80, 40) });
            diagram.Add(new DiagramNode { Id = "b", Type = "service", Bounds = new DiagramRect(300, 0, 80, 40) });
            diagram.Connect(new DiagramLink { Id = "ab", Type = "flow", Source = new DiagramLinkEnd("a", "out"), Target = new DiagramLinkEnd("b", "in") });
        });
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(controller.Invocations["applyChanges"]));
        var sent = controller.Invocations["applyChanges"].Count;
        var version = diagram.Snapshot.Version;

        await cut.InvokeAsync(router.Answer);
        await cut.WaitForAssertionAsync(() => Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("ab")!).RouteState));
        await cut.InvokeAsync(static () => { });

        Assert.True(diagram.Snapshot.Version > version);
        Assert.Equal(sent, controller.Invocations["applyChanges"].Count);
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
    public async Task AnExportEndsQuietlyWhenTheViewIsGoneWhileTheScriptExports()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var exporting = controller.Setup<string>("exportDiagram", static _ => true);
        controller.Setup<IJSStreamReference>("takeExport", static _ => true).SetException(new ObjectDisposedException("controller"));
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        Task<DiagramExportResult>? export = null;

        await cut.InvokeAsync(() =>
        {
            export = cut.Instance.ExportImageAsync();
        });
        await cut.WaitForAssertionAsync(() => controller.VerifyInvoke("exportDiagram"));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => exporting.SetResult("{\"missing\":[],\"empty\":false,\"tooLarge\":false,\"id\":1}"));
        var result = await export!.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Null(result.Content);
        Assert.Empty(controller.Invocations["takeExport"]);
    }

    [Fact]
    public async Task SelectingSendsTheScriptTheSelectionAsItStandsAfterTheHandlers()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(p => p.SelectionChanged, ids => ids is ["a"] ? release.Task : Task.CompletedTask));
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(controller.Invocations["ready"]));

        var selecting = cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());
        await cut.InvokeAsync(() => new DiagramScriptBridge(cut.Instance).OnSelectionChangedAsync(["b"], 1));
        release.SetResult();
        await selecting.ConfigureAwait(true);

        Assert.Equal(["b"], cut.Instance.SelectedIds);
        Assert.Equal(["b"], (IEnumerable<string>)controller.Invocations["select"][^1].Arguments[0]!);
    }

    [Fact]
    public async Task CSharpSelectsWithTheNumberOfTheLastMessageFromTheScript()
    {
        var controller = module.SetupModule("attach", static _ => true);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(controller.Invocations["ready"]));
        var bridge = new DiagramScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.OnSelectionChangedAsync(["a"], 4));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["b"]).AsTask());
        var afterSelection = controller.Invocations["select"][^1].Arguments[1];
        await cut.InvokeAsync(() => bridge.ExecuteCommandAsync(DiagramCommands.Copy, null, 7));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(["a"]).AsTask());

        Assert.Equal(4, afterSelection);
        Assert.Equal(7, controller.Invocations["select"][^1].Arguments[1]);
    }

    [Fact]
    public async Task ACommandFromTheScriptActsOnTheSelectionItBrought()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagram = CreateDiagram();
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, diagram)
            .Add(p => p.SelectionChanged, ids => ids is ["b"] ? release.Task : Task.CompletedTask));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var deleting = cut.InvokeAsync(() => bridge.ExecuteCommandAsync(DiagramCommands.Delete, ["b"], 1));
        await cut.InvokeAsync(() => bridge.OnSelectionChangedAsync(["vpc"], 2));
        release.SetResult();
        await deleting.ConfigureAwait(true);

        Assert.False(diagram.Snapshot.Contains("b"));
        Assert.True(diagram.Snapshot.Contains("vpc"));
    }

    [Fact]
    public async Task DisposingWhileTheScriptIsAttachingReleasesWhatAttachReturns()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Hold = "attach", Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.True(instance.Released));
        Assert.False(Renderer.UnhandledException.IsCompleted);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task DisposingWhileTheCommandsAreSentStopsTheRest()
    {
        var runtime = new LateRuntime { Module = { Hold = "setCommands" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var diagram = new Diagram(Schema with { Rules = new DiagramAppRules { InPalette = static _ => true } });
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("setCommands", runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        Assert.False(Renderer.UnhandledException.IsCompleted);
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
    public async Task DisposingTellsTheScriptAndReleasesIt()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", instance.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Equal(["attach"], runtime.Module.Calls);
        Assert.Contains("dispose", instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task AFailedDisposeStillReleasesTheScript()
    {
        var instance = new LateModule { Fail = "dispose" };
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", instance.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Contains("dispose", instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
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
    public async Task AFailedCallBeforeTheScriptIsAttachedDoesNotStopTheRest()
    {
        var runtime = new LateRuntime { Module = { Fail = "fit" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));

        await cut.InvokeAsync(() => cut.Instance.ZoomToFitAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.CenterOnAsync("b").AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", runtime.Module.Calls));
        Assert.Equal(["fit", "centerOn", "ready"], runtime.Module.Calls.Where(static x => x is "fit" or "centerOn" or "ready"));
        Assert.False(Renderer.UnhandledException.IsCompleted);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcesStillReleaseTheScriptWhenTheirDisposeFails(bool palette)
    {
        var instance = new LateModule { Fail = "dispose" };
        var runtime = new LateRuntime { Module = { Hold = "attach", Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var dispose = palette ? RenderPalette() : RenderSource();
        await view.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await view.WaitForAssertionAsync(() => Assert.Contains("attachSource", runtime.Module.Calls));

        await dispose();

        Assert.Contains("dispose", instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);

        Func<Task> RenderPalette()
        {
            var cut = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));
            return () => cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        }

        Func<Task> RenderSource()
        {
            var cut = Render<MusselDiagramDragSource>(parameters => parameters.Add(p => p.View, view.Instance).Add(static p => p.Type, "service"));
            return () => cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcesReleaseTheScriptThatAttachesAfterTheyAreGone(bool palette)
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Hold = "attachSource", Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var dispose = palette ? RenderPalette() : RenderSource();
        await view.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await view.WaitForAssertionAsync(() => Assert.Contains("attachSource", runtime.Module.Calls));

        await dispose();
        await view.InvokeAsync(runtime.Module.Resume.SetResult);

        await view.WaitForAssertionAsync(() => Assert.True(instance.Released));
        Assert.False(Renderer.UnhandledException.IsCompleted);
        Assert.Contains("dispose", instance.Calls);
        Assert.True(runtime.Module.Released);

        Func<Task> RenderPalette()
        {
            var cut = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));
            return () => cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        }

        Func<Task> RenderSource()
        {
            var cut = Render<MusselDiagramDragSource>(parameters => parameters.Add(p => p.View, view.Instance).Add(static p => p.Type, "service"));
            return () => cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task AnExceptionInAHandlerCalledFromTheScriptReachesTheRenderer()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(static p => p.SelectionChanged, static _ => throw new InvalidOperationException("app")));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => bridge.OnSelectionChangedAsync(["a"], 1)));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public async Task CanceledHandlersAndLostConnectionsDoNotReachTheRenderer()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters
            .Add(static p => p.Diagram, CreateDiagram())
            .Add(static p => p.SelectionChanged, static _ => throw new OperationCanceledException())
            .Add(static p => p.Moving, static _ => throw new JSDisconnectedException("gone")));

        var bridge = new DiagramScriptBridge(cut.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.OnSelectionChangedAsync(["a"], 1)));
        await Assert.ThrowsAsync<JSDisconnectedException>(() => cut.InvokeAsync(() => bridge.CommitMoveAsync(["a"], 10, 0, false, null, null, cut.Instance.ScriptVersion, 0, null, 1)));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.NotSame(Renderer.UnhandledException, ended);
    }

    [Fact]
    public async Task AFailureAfterTheViewIsGoneDoesNotReachTheRenderer()
    {
        var pending = new TaskCompletionSource();
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(p => p.SelectionChanged, async _ =>
        {
            await pending.Task.ConfigureAwait(true);
            throw new InvalidOperationException("late");
        }));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var changed = cut.InvokeAsync(() => bridge.OnSelectionChangedAsync(["a"], 1));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        pending.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => changed);
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.NotSame(Renderer.UnhandledException, ended);
    }

    [Fact]
    public async Task ASearchFromTheScriptAfterTheViewIsGoneIsCanceled()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        var bridge = new DiagramScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.SearchTypesAsync("a")));
    }

    [Fact]
    public async Task AnExceptionInTheSearchNormalizerReachesTheRenderer()
    {
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()).Add(static p => p.SearchNormalizer, static _ => throw new InvalidOperationException("app")));
        var bridge = new DiagramScriptBridge(cut.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => bridge.SearchTypesAsync("a")));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.IsType<InvalidOperationException>(await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public async Task ASearchFromTheScriptRanksEveryTypeInThePaletteOrder()
    {
        var schema = new DiagramSchema
        {
            NodeTypes =
            [
                new DiagramNodeType { Name = "server", DisplayName = "Web server", Category = "b" },
                new DiagramNodeType { Name = "printer", DisplayName = "Server room printer", InPalette = false },
                new DiagramNodeType { Name = "plain", DisplayName = "Server" }
            ],
            GroupTypes = [new DiagramGroupType { Name = "servers", DisplayName = "Servers", Category = "a" }],
            Categories = [new DiagramCategory { Name = "a" }, new DiagramCategory { Name = "b" }]
        };
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(schema)).Add(static p => p.SearchNormalizer, static x => x.Replace("ｓ", "s", StringComparison.Ordinal)));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var names = await cut.InvokeAsync(() => bridge.SearchTypesAsync("ｓerver"));

        Assert.Equal(["plain", "printer", "servers", "server"], names.EnumerateArray().Select(static x => x.GetString()));
    }

    [Fact]
    public async Task ExportingFromADisposedViewSaysSo()
    {
        module.SetupModule("attach", static _ => true);
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, CreateDiagram()));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => cut.Instance.ExportSvgAsync()));

        Assert.Equal("The view is disposed.", error.Message);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnExceptionFromTheContentOfANewBoxReachesTheRenderer(bool place)
    {
        var schema = new DiagramSchema { NodeTypes = [new DiagramNodeType { Name = "broken", CreateContent = static _ => throw new InvalidOperationException("app") }] };
        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(schema)));
        var bridge = new DiagramScriptBridge(cut.Instance);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => place
            ? bridge.CommitPlaceAsync("broken", 0, 0, 80, 40, null, "a", null, "flow", 0, [], 1)
            : bridge.CommitDropAsync("broken", 0, 0, 80, 40, null, null, null, default, 0, [], 1)));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
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

    private sealed class HeldRouter : IDiagramRouter
    {
        private readonly List<(TaskCompletionSource<DiagramRouteResult> Completion, DiagramRouteRequest Request)> held = [];

        public ValueTask<DiagramRouteResult> RouteAsync(DiagramRouteRequest request, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<DiagramRouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Add((completion, request));
            return new ValueTask<DiagramRouteResult>(completion.Task);
        }

        public void Answer()
        {
            foreach (var (completion, request) in held)
            {
                completion.TrySetResult(new DiagramRouteResult { Points = [request.Source.Position, request.Target.Position] });
            }
        }
    }

    public sealed class ViewportHost : ComponentBase
    {
        [Parameter]
        public Diagram Diagram { get; set; } = default!;

        public double Zoom { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MusselDiagramView>(0);
            builder.AddComponentParameter(1, nameof(MusselDiagramView.Diagram), Diagram);
            builder.AddComponentParameter(2, nameof(MusselDiagramView.ViewportChanged), EventCallback.Factory.Create<DiagramViewport>(this, OnViewportChanged));
            builder.CloseComponent();
        }

        private void OnViewportChanged(DiagramViewport viewport) => Zoom = viewport.Zoom;
    }

    public sealed class PaletteFirstHost : ComponentBase
    {
        private MusselDiagramView? view;

        [Parameter]
        public Diagram Diagram { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (view is not null)
            {
                builder.OpenComponent<MusselDiagramPalette>(0);
                builder.AddComponentParameter(1, nameof(MusselDiagramPalette.View), view);
                builder.CloseComponent();
            }

            builder.OpenComponent<MusselDiagramView>(2);
            builder.AddComponentParameter(3, nameof(MusselDiagramView.Diagram), Diagram);
            builder.AddComponentReferenceCapture(4, x => view = (MusselDiagramView)x);
            builder.CloseComponent();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                StateHasChanged();
            }
        }
    }
}
