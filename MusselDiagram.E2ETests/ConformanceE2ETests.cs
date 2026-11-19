namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ConformanceE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private SamplePage? opened;

    public static TheoryData<string> Modes => ["server", "wasm"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ThePickerFollowsThePaletteAndClosesFromOutside(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var picker = opened.View.Locator(".mussel-diagram-picker");
        await opened.View.FocusAsync();

        await opened.Page.Keyboard.PressAsync("Control+Enter");

        await Expect(picker).ToBeVisibleAsync();
        var names = await picker.Locator(".mussel-diagram-picker-item").EvaluateAllAsync<string[]>("items => items.map((x) => x.getAttribute('data-type'))");
        Assert.Equal(["rectangle", "ellipse", "diamond", "hexagon", "card", "star", "triangle", "pin", "frame", "group", "dashed"], names);
        await Expect(picker.Locator(".mussel-diagram-picker-item[data-type='diamond'] polygon.mussel-diagram-picker-shape")).ToHaveCountAsync(1);

        await opened.Page.Mouse.ClickAsync(4, 4);
        await Expect(picker).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ThePickerScrollsOnlyItsOwnList(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var picker = opened.View.Locator(".mussel-diagram-picker");
        await opened.Page.SetViewportSizeAsync(1280, 360);
        await opened.View.FocusAsync();
        await opened.Page.Keyboard.PressAsync("Control+Enter");
        await Expect(picker).ToBeVisibleAsync();

        for (var i = 0; i < 9; i++)
        {
            await opened.Page.Keyboard.PressAsync("ArrowDown");
        }

        await Expect(picker.Locator(".mussel-diagram-picker-item").Nth(9)).ToHaveAttributeAsync("aria-selected", "true");
        Assert.Equal(0, await opened.View.EvaluateAsync<double>("view => view.scrollTop"));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task RejectedPortsShowTheReasonWhileAndAfterDragging(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(7);

        await DragAsync(430, 190, -100, 0, async () =>
        {
            await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='rect'] circle[data-port='in'][data-not-allowed]")).ToHaveCountAsync(1).ConfigureAwait(true);
            await Expect(opened.View).ToHaveClassAsync(new Regex("mussel-diagram-not-allowed")).ConfigureAwait(true);
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveAttributeAsync("marker-end", new Regex("^url\\(#")).ConfigureAwait(true);
        });

        await Expect(opened.View.Locator(".mussel-diagram-reason")).ToBeVisibleAsync();
        await Expect(opened.View).Not.ToHaveClassAsync(new Regex("mussel-diagram-not-allowed"));
        await Expect(links).ToHaveCountAsync(7);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ACoverHoldsTheCursorOnlyDuringAnOperation(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var cover = opened.View.Locator(".mussel-diagram-cursor");
        await Expect(opened.View.Locator("g.mussel-diagram-link")).ToHaveCountAsync(7);
        await Expect(cover).ToBeHiddenAsync();

        await DragAsync(430, 190, -100, 0, async () =>
        {
            await Expect(cover).ToBeVisibleAsync().ConfigureAwait(true);
            await Expect(cover).ToHaveCSSAsync("cursor", "not-allowed").ConfigureAwait(true);
            Assert.True(await cover.EvaluateAsync<bool>("cover => cover.hasPointerCapture(1)").ConfigureAwait(true));
        });

        await Expect(cover).ToBeHiddenAsync();

        await DragAsync(250, 170, 60, 40, async () =>
        {
            await Expect(cover).ToHaveCSSAsync("cursor", "move").ConfigureAwait(true);
            Assert.True(await cover.EvaluateAsync<bool>("cover => cover.hasPointerCapture(1)").ConfigureAwait(true));
        });

        await Expect(cover).ToBeHiddenAsync();
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CommandKeysWaitUntilTheDragEnds(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");

        await DragAsync(250, 170, 60, 40, async () => await opened.Page.Keyboard.PressAsync("Delete").ConfigureAwait(true));

        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");
        await Expect(star1).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Delete");
        await Expect(star1).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task KeysOutsideTypingWorkWhileEditingText(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var view = opened.View;
        var model = await view.Locator("g.mussel-diagram-node[data-id='model'] > .mussel-diagram-shape").BoundingBoxAsync() ?? throw new InvalidOperationException("The node is not shown.");
        await opened.Page.Mouse.ClickAsync(model.X + (model.Width / 2), model.Y + (model.Height / 2));
        await Expect(view.Locator("g.mussel-diagram-node[data-id='model'][data-selected]")).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Delete");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='agent']")).ToHaveClassAsync(new Regex("mussel-diagram-error"));
        var label = await view.Locator("g.mussel-diagram-node[data-id='reply'] > text.mussel-diagram-label").BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");

        await opened.Page.Mouse.DblClickAsync(label.X + (label.Width / 2), label.Y + (label.Height / 2));
        var editor = view.Locator("textarea.mussel-diagram-text-editor");
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("F8");

        await Expect(view.Locator("g.mussel-diagram-node[data-id='agent'][data-selected]")).ToHaveCountAsync(1);
        await Expect(view.Locator("g.mussel-diagram-node[data-id='reply'][data-selected]")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TheTextEditorKeepsItsOwnKeys(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        await DragAsync(250, 170, 60, 40, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");
        var moved = await star1.GetAttributeAsync("transform");
        await page.Keyboard.PressAsync("Control+Z");
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");

        await page.Keyboard.PressAsync("F2");
        await Expect(editor).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Control+Shift+Z");
        await page.WaitForTimeoutAsync(500);
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
        await page.Keyboard.PressAsync("Escape");
        await Expect(editor).ToHaveCountAsync(0);
        await page.Keyboard.PressAsync("Control+Shift+Z");
        await Expect(star1).ToHaveAttributeAsync("transform", moved!);

        await page.Keyboard.PressAsync("F2");
        await Expect(editor).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync("x");
        await page.Keyboard.PressAsync("Control+Enter");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator(".mussel-diagram-picker")).ToHaveCountAsync(0);
        await Expect(star1.Locator("text.mussel-diagram-label[data-label='星 1x']")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ARenderOfAnEarlierMoveKeepsTheLaterMoveShown()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """);

        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await DragAsync(310, 170, 0, 40, null);
        var shown = await star1.GetAttributeAsync("transform");
        var version = await opened.View.GetAttributeAsync("data-version");
        await page.EvaluateAsync("() => window.heldMoves[0]()");
        await Expect(opened.View).Not.ToHaveAttributeAsync("data-version", version!);
        var afterEarlier = await star1.GetAttributeAsync("transform");
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 2");
        await page.EvaluateAsync("() => window.heldMoves[1]()");

        Assert.Equal(shown, afterEarlier);
        await Expect(star1).ToHaveAttributeAsync("transform", shown!);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AMovedLinkShowsTheRouteOfItsOwnRender(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var line = opened.View.Locator("g.mussel-diagram-link[data-id='star1-triangle'] > path.mussel-diagram-link-line");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.answeredMoves = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    const result = invoke.call(this, name, ...args);
                    if (name === 'CommitMoveAsync') {
                        result.then(() => setTimeout(() => window.answeredMoves++));
                    }
                    return result;
                };
            }
            """);

        await DragAsync(250, 170, 0, 20, null);
        await page.WaitForFunctionAsync("() => window.answeredMoves === 1");
        var shown = await line.GetAttributeAsync("d");
        await page.Keyboard.PressAsync("Control+Z");
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
        await page.Keyboard.PressAsync("Control+Y");
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");

        await Expect(line).ToHaveAttributeAsync("d", shown!);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AnEscapedDragKeepsWhatARenderDrewDuringIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var line = opened.View.Locator("g.mussel-diagram-link[data-id='star1-triangle'] > path.mussel-diagram-link-line");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """);

        await DragAsync(250, 170, 0, 20, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        var shown = await line.GetAttributeAsync("d");
        var (left, top) = await OriginAsync();
        string? rendered = null;
        await DragAsync(250, 190, 30, 0, async () =>
        {
            var version = await opened.View.GetAttributeAsync("data-version").ConfigureAwait(true);
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await Expect(opened.View).Not.ToHaveAttributeAsync("data-version", version!).ConfigureAwait(true);
            rendered = await line.GetAttributeAsync("d").ConfigureAwait(true);
            await page.Mouse.MoveAsync((float)left + 290, (float)top + 190).ConfigureAwait(true);
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(true);
        });

        Assert.NotEqual(shown, rendered);
        await Expect(line).ToHaveAttributeAsync("d", rendered!);
    }

    private async Task<(double X, double Y)> OriginAsync()
    {
        var box = await opened!.View.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The view is not shown.");
        return (box.X, box.Y);
    }

    [Fact]
    public async Task ACommandThatWaitedSendsTheCurrentSelectionAfterIt()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                window.calls = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if ((name === 'ExecuteCommandAsync') || (name === 'OnSelectionChangedAsync')) {
                        window.calls.push(name + ' ' + JSON.stringify(args[args.length - 1]));
                    }
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """);

        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await page.Keyboard.PressAsync("Control+L");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await page.WaitForFunctionAsync("() => window.calls.some((x) => x.startsWith('OnSelectionChangedAsync') && x.includes('rect'))");
        await page.EvaluateAsync("() => window.heldMoves[0]()");
        await page.WaitForFunctionAsync("() => window.calls.some((x) => x.startsWith('ExecuteCommandAsync'))");

        await page.WaitForFunctionAsync(
            "() => window.calls[window.calls.length - 1].startsWith('OnSelectionChangedAsync') && window.calls[window.calls.length - 1].includes('rect')",
            null,
            new() { Timeout = 5000 });
    }

    [Fact]
    public async Task ACommandThatChangesTheSelectionSendsNoSelectionBack()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.calls = [];
                window.commandsDone = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    window.calls.push(name);
                    const result = invoke.call(this, name, ...args);
                    if (name === 'ExecuteCommandAsync') {
                        result.then(() => setTimeout(() => window.commandsDone++, 0), () => { });
                    }
                    return result;
                };
            }
            """);
        var start = await star1.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await DragAsync(250, 170, 60, 0, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", start);
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.EvaluateAsync("() => { window.calls = []; }");

        await page.Keyboard.PressAsync("Control+Z");
        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.WaitForFunctionAsync("() => window.commandsDone === 1");

        var calls = await page.EvaluateAsync<string[]>("() => window.calls");
        Assert.Contains("ExecuteCommandAsync", calls);
        Assert.DoesNotContain("OnSelectionChangedAsync", calls);
    }

    [Fact]
    public async Task ACommandResendsASelectionThatArrivedBeforeItWasSent()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldUndo = null;
                window.selected = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if ((name === 'ExecuteCommandAsync') && (args[0] === 'Undo') && !window.heldUndo) {
                        return new Promise((resolve) => { window.heldUndo = () => resolve(invoke.call(this, name, ...args)); });
                    }
                    const result = invoke.call(this, name, ...args);
                    if (name === 'OnSelectionChangedAsync') {
                        result.then(() => window.selected.push(JSON.stringify(args[0])), () => { });
                    }
                    return result;
                };
            }
            """);
        var start = await star1.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await DragAsync(250, 170, 60, 0, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", start);
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.WaitForFunctionAsync("() => window.selected.includes(JSON.stringify(['rect']))");
        await page.EvaluateAsync("() => { window.selected = []; }");

        await page.Keyboard.PressAsync("Control+Z");
        await page.WaitForFunctionAsync("() => window.heldUndo !== null");
        await page.Keyboard.PressAsync("Delete");
        await page.EvaluateAsync("() => window.heldUndo()");
        await Expect(rect).ToHaveCountAsync(0);
        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.WaitForFunctionAsync("() => window.selected.includes(JSON.stringify(['star1']))", null, new() { Timeout = 5000 });
        await page.Keyboard.PressAsync("Delete");

        await Expect(star1).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AResizeStartedOnARejectedMoveResizesTheBoxWhereItWentBack()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var shape = opened.View.Locator("g.mussel-diagram-node[data-id='rect'] > rect.mussel-diagram-shape");
        var start = await rect.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, args[0], 2e12, ...args.slice(2)))));
                };
            }
            """);

        await DragAsync(380, 190, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(rect).Not.ToHaveAttributeAsync("transform", start);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-handle]")).ToHaveCountAsync(8);
        await DragAsync(490, 220, 30, 70, async () =>
        {
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await Expect(rect).ToHaveAttributeAsync("transform", start).ConfigureAwait(true);
        });

        await Expect(shape).ToHaveAttributeAsync("width", "130");
        await Expect(rect).ToHaveAttributeAsync("transform", start);
        await Expect(shape).ToHaveAttributeAsync("height", "130");
    }

    [Fact]
    public async Task AMoveStartedOnARejectedMoveSnapsFromWhereItWentBack()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        Assert.Equal("translate(330,160)", await rect.GetAttributeAsync("transform"));
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                window.movesDone = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if ((name !== 'CommitMoveAsync') || (window.heldMoves.length > 0)) {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => {
                        const result = invoke.call(this, name, args[0], 2e12, ...args.slice(2));
                        result.then(() => setTimeout(() => window.movesDone++, 0), () => { });
                        resolve(result);
                    }));
                };
            }
            """);

        await page.Keyboard.DownAsync("Alt");
        await DragAsync(380, 190, 63, 0, null);
        await page.Keyboard.UpAsync("Alt");
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(rect).ToHaveAttributeAsync("transform", "translate(393,160)");
        await DragAsync(443, 190, 40, 0, async () =>
        {
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.movesDone === 1").ConfigureAwait(true);
            await Expect(rect).ToHaveAttributeAsync("transform", "translate(370,156)").ConfigureAwait(true);
        });

        await Expect(rect).ToHaveAttributeAsync("transform", "translate(370,156)");
    }

    [Fact]
    public async Task AResizeGrabbedWhileALaterMoveWaitsKeepsThatMove()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var shape = opened.View.Locator("g.mussel-diagram-node[data-id='rect'] > rect.mussel-diagram-shape");
        Assert.Equal("translate(330,160)", await rect.GetAttributeAsync("transform"));
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """);

        await page.Keyboard.DownAsync("Alt");
        await DragAsync(380, 190, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await DragAsync(440, 190, 40, 0, null);
        await Expect(rect).ToHaveAttributeAsync("transform", "translate(430,160)");
        await page.EvaluateAsync("() => window.heldMoves[0]()");
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 2");
        await page.WaitForFunctionAsync("() => { const handle = document.querySelector(\".mussel-diagram [data-handle='nw']\"); return (handle !== null) && (Number(handle.getAttribute('x')) + (Number(handle.getAttribute('width')) / 2) === 390); }");
        await DragAsync(390, 160, -30, -20, null);
        await page.Keyboard.UpAsync("Alt");
        await page.EvaluateAsync("() => window.heldMoves[1]()");

        await Expect(shape).ToHaveAttributeAsync("width", "130");
        await Expect(shape).ToHaveAttributeAsync("height", "80");
        await Expect(rect).ToHaveAttributeAsync("transform", "translate(400,140)");
    }

    [Fact]
    public async Task AResizeStopsWhenARejectedReconnectTakesALinkOffTheBox()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var hit = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1'] > path.mussel-diagram-link-hit");
        var shape = opened.View.Locator("g.mussel-diagram-node[data-id='rect'] > rect.mussel-diagram-shape");
        var path = await hit.GetAttributeAsync("d") ?? throw new InvalidOperationException("The link has no path.");
        var width = await shape.GetAttributeAsync("width") ?? throw new InvalidOperationException("The node has no width.");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldReconnects = [];
                window.reconnectsDone = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitReconnectAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldReconnects.push(() => {
                        const result = invoke.call(this, name, args[0], args[1], 'missing', ...args.slice(3));
                        result.then(() => setTimeout(() => window.reconnectsDone++, 0), () => { });
                        resolve(result);
                    }));
                };
            }
            """);
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 200, (float)top + 170);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-end='target'][data-id='pin-star1']")).ToHaveCountAsync(1);
        await DragAsync(226, 170, 104, 20, null);
        await page.WaitForFunctionAsync("() => window.heldReconnects.length === 1");
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1']")).ToHaveAttributeAsync("data-reconnecting", string.Empty);
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-handle]")).ToHaveCountAsync(8);

        await DragAsync(430, 220, 30, 70, async () =>
        {
            await page.EvaluateAsync("() => window.heldReconnects[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.reconnectsDone === 1").ConfigureAwait(true);
        });

        await Expect(hit).ToHaveAttributeAsync("d", path);
        await Expect(shape).ToHaveAttributeAsync("width", width);
    }

    private async Task DragAsync(float x, float y, float dx, float dy, Func<Task>? beforeRelease)
    {
        var page = opened!.Page;
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        var startX = (float)left + x;
        var startY = (float)top + y;
        await page.Mouse.MoveAsync(startX, startY).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(startX + dx, startY + dy, new() { Steps = 10 }).ConfigureAwait(true);
        if (beforeRelease is not null)
        {
            await beforeRelease().ConfigureAwait(true);
        }

        await page.Mouse.UpAsync().ConfigureAwait(true);
    }
}
