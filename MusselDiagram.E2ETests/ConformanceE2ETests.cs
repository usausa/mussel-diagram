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
                        window.calls.push(name + ' ' + JSON.stringify(args[args.length - 2]));
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

    [Theory]
    [InlineData(2e12, 226)]
    [InlineData(100, 326)]
    public async Task TheTextEditorFollowsItsElementToWhereTheAnswerOfAMovePutsIt(double dx, int x)
    {
        const string offset =
            """
            () => new Promise((resolve) => setTimeout(() => {
                const editor = document.querySelector('textarea.mussel-diagram-text-editor').getBoundingClientRect();
                const label = document.querySelector("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label").getBoundingClientRect();
                resolve((editor.left + (editor.width / 2)) - (label.left + (label.width / 2)));
            }, 300))
            """;
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await page.EvaluateAsync(
            """
            dx => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitMoveAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldMoves.push(() => resolve(invoke.call(this, name, args[0], dx, 0, ...args.slice(3)))));
                };
            }
            """,
            dx);
        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        var label = await opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label").BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");
        await page.Mouse.DblClickAsync(label.X + (label.Width / 2), label.Y + (label.Height / 2));
        await Expect(opened.View.Locator("textarea.mussel-diagram-text-editor")).ToBeVisibleAsync();
        var aligned = await page.EvaluateAsync<double>(offset);

        await page.EvaluateAsync("() => window.heldMoves[0]()");

        await Expect(star1).ToHaveAttributeAsync("transform", $"translate({x},146)");
        Assert.InRange(await page.EvaluateAsync<double>(offset), aligned - 1, aligned + 1);
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

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task AMoveStopsUntilTheReleaseWhenARejectedReconnectTakesALinkOffTheBox(bool live, bool moveOn)
    {
        if (live)
        {
            opened = await app.OpenAsync(string.Empty, "server");
        }
        else
        {
            opened = await OpenWithFramedMovesAsync();
        }

        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var star2 = opened.View.Locator("g.mussel-diagram-node[data-id='star2']");
        var hit = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1'] > path.mussel-diagram-link-hit");
        var path = await hit.GetAttributeAsync("d") ?? throw new InvalidOperationException("The link has no path.");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldReconnects = [];
                window.reconnectsDone = 0;
                window.clicks = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name === 'OnClickedAsync') {
                        window.clicks++;
                    }
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
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await page.Keyboard.DownAsync("Shift");
        await page.Mouse.ClickAsync((float)left + 250, (float)top + 300);
        await page.Keyboard.UpAsync("Shift");
        await Expect(star2).ToHaveAttributeAsync("data-selected", string.Empty);
        var clicks = 0;

        await DragAsync(380, 190, 60, 40, async () =>
        {
            await page.EvaluateAsync("() => window.heldReconnects[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.reconnectsDone === 1").ConfigureAwait(true);
            clicks = await page.EvaluateAsync<int>("() => window.clicks").ConfigureAwait(true);
            if (moveOn)
            {
                await page.Mouse.MoveAsync((float)left + 480, (float)top + 260, new() { Steps = 5 }).ConfigureAwait(true);
            }
        });

        await Expect(hit).ToHaveAttributeAsync("d", path);
        await Expect(rect).ToHaveAttributeAsync("transform", "translate(330,160)");
        await Expect(star2).ToHaveAttributeAsync("transform", "translate(226,276)");
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(star2).ToHaveAttributeAsync("data-selected", string.Empty);
        Assert.Equal(clicks, await page.EvaluateAsync<int>("() => new Promise((resolve) => setTimeout(() => resolve(window.clicks), 300))"));
    }

    [Fact]
    public async Task AGroupMoveStopsWhenARejectedMoveTakesANodeBackOutOfTheGroup()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var group = opened.View.Locator("g.mussel-diagram-group[data-id='right']");
        await HoldTheFirstMoveAsync();
        await DragAsync(380, 190, 200, 40, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(rect).Not.ToHaveAttributeAsync("transform", "translate(330,160)");
        var label = await opened.View.Locator("g.mussel-diagram-group[data-id='right'] > text.mussel-diagram-label").BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");
        var (left, top) = await OriginAsync();

        await DragAsync((float)(label.X + 10 - left), (float)(label.Y + (label.Height / 2) - top), 0, 20, async () =>
        {
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.movesDone === 1").ConfigureAwait(true);
        });

        await Expect(rect).ToHaveAttributeAsync("transform", "translate(330,160)");
        await Expect(group).ToHaveAttributeAsync("transform", "translate(470,100)");
    }

    [Fact]
    public async Task TheFrameOfAMoveTakesTheSizeOfWhereTheElementsWentBack()
    {
        opened = await OpenWithFramedMovesAsync();
        var page = opened.Page;
        var star2 = opened.View.Locator("g.mussel-diagram-node[data-id='star2']");
        var frame = opened.View.Locator(".mussel-diagram-drag-frame");
        await HoldTheFirstMoveAsync();
        await page.Keyboard.DownAsync("Alt");
        await DragAsync(250, 300, 0, 60, null);
        await page.Keyboard.UpAsync("Alt");
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(star2).ToHaveAttributeAsync("transform", "translate(226,336)");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await page.Keyboard.DownAsync("Shift");
        await page.Mouse.ClickAsync((float)left + 250, (float)top + 360);
        await page.Keyboard.UpAsync("Shift");
        await Expect(star2).ToHaveAttributeAsync("data-selected", string.Empty);

        await DragAsync(380, 190, 40, 0, async () =>
        {
            await Expect(frame).ToHaveAttributeAsync("height", "224").ConfigureAwait(true);
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.movesDone === 1").ConfigureAwait(true);
            await Expect(frame).ToHaveAttributeAsync("height", "164").ConfigureAwait(true);
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(true);
        });
    }

    [Fact]
    public async Task AMoveMarksItsDropTargetAgainWhenARejectionTakesTheNodeBackOutOfTheGroup()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var group = opened.View.Locator("g.mussel-diagram-group[data-id='right']");
        await HoldTheFirstMoveAsync();
        await DragAsync(380, 190, 200, 40, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");

        await DragAsync(580, 230, 20, 0, async () =>
        {
            await Expect(group).Not.ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true);
            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.movesDone === 1").ConfigureAwait(true);
            await Expect(group).ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true);
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(true);
        });
    }

    [Theory]
    [InlineData(350)]
    [InlineData(240)]
    public async Task AMoveDropsItsTargetLookWhenAFailedMoveLeavesItsElementsInTwoParents(float dy)
    {
        opened = await OpenWithScriptArgumentsChangedAsync("(item) => { for (const type of Array.isArray(item.types) ? item.types : []) { if (type.name === 'star') { type.topLevel = false; } } }");
        var page = opened.Page;
        var triangle = opened.View.Locator("g.mussel-diagram-node[data-id='triangle']");
        var group = opened.View.Locator("g.mussel-diagram-group[data-id='group']");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldMoves = [];
                window.movesDone = 0;
                window.moveCalls = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name === 'CommitMoveAsync') {
                        window.moveCalls++;
                    }
                    if ((name !== 'CommitMoveAsync') || (window.heldMoves.length > 0)) {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve, reject) => window.heldMoves.push(() => {
                        reject(new Error(name));
                        setTimeout(() => window.movesDone++, 0);
                    }));
                };
            }
            """);
        await page.Keyboard.DownAsync("Alt");
        await DragAsync(600, 230, -220, 100, null);
        await page.Keyboard.UpAsync("Alt");
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(triangle).ToHaveAttributeAsync("transform", "translate(356,306)");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 250, (float)top + 170);
        await page.Keyboard.DownAsync("Shift");
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 330);
        await page.Keyboard.UpAsync("Shift");
        await Expect(triangle).ToHaveAttributeAsync("data-selected", string.Empty);

        await DragAsync(250, 170, 0, dy, async () =>
        {
            if (dy > 300)
            {
                await Expect(opened.View).ToHaveClassAsync(new Regex("mussel-diagram-not-allowed")).ConfigureAwait(true);
            }
            else
            {
                await Expect(group).ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true);
            }

            await page.EvaluateAsync("() => window.heldMoves[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.movesDone === 1").ConfigureAwait(true);
            await Expect(opened.View).Not.ToHaveClassAsync(new Regex("mussel-diagram-not-allowed")).ConfigureAwait(true);
            await Expect(opened.View.Locator(".mussel-diagram-reason")).ToHaveCountAsync(0).ConfigureAwait(true);
            await Expect(group).Not.ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true);
        });

        await page.WaitForFunctionAsync("() => window.moveCalls === 2");
    }

    [Fact]
    public async Task ASelectionOfCSharpThatCrossesAClickLeavesTheClickedLink()
    {
        opened = await OpenHoldingTheSelectionsOfCSharpAsync();
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var link = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1']");
        await DragAsync(250, 170, 60, 0, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");
        await page.EvaluateAsync("() => { window.holdSelects = true; }");
        await page.Keyboard.PressAsync("Control+Z");
        await page.WaitForFunctionAsync("() => window.heldSelects.length === 1");
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 200, (float)top + 170);
        await Expect(link).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(page.Locator("[data-panel='look'] [data-field='link-dash']")).ToHaveCountAsync(1);

        await page.EvaluateAsync("() => window.heldSelects[0]()");

        await Expect(link).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(star1).Not.ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(page.Locator("[data-panel='look'] [data-field='link-dash']")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ASelectionOfCSharpThatCrossesAMoveLeavesTheMovedNode()
    {
        opened = await OpenHoldingTheSelectionsOfCSharpAsync();
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var link = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1']");
        var hit = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1'] > path.mussel-diagram-link-hit");
        var path = await hit.GetAttributeAsync("d") ?? throw new InvalidOperationException("The link has no path.");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 200, (float)top + 170);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-end='target'][data-id='pin-star1']")).ToHaveCountAsync(1);
        await DragAsync(226, 170, 104, 20, null);
        await Expect(hit).Not.ToHaveAttributeAsync("d", path);
        await Expect(link).Not.ToHaveAttributeAsync("data-reconnecting", string.Empty);
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(page.Locator("[data-panel='look'] [data-field='box-fill']")).ToHaveCountAsync(1);
        await page.EvaluateAsync("() => { window.holdSelects = true; }");
        await page.Keyboard.PressAsync("Control+Z");
        await page.WaitForFunctionAsync("() => window.heldSelects.length === 1");

        await DragAsync(380, 190, 40, 0, null);
        await Expect(rect).Not.ToHaveAttributeAsync("transform", "translate(330,160)");
        await page.EvaluateAsync("() => window.heldSelects[0]()");

        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(link).Not.ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(page.Locator("[data-panel='look'] [data-field='box-fill']")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-panel='look'] [data-field='link-dash']")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AMoveStopsUntilTheReleaseWhenItsNodeIsDeleted(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var errors = new List<string>();
        page.PageError += (_, e) => errors.Add(e);
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var cover = opened.View.Locator(".mussel-diagram-cursor");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldCommands = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'ExecuteCommandAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    return new Promise((resolve) => window.heldCommands.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """);
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 250, (float)top + 170);
        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Keyboard.PressAsync("Delete");
        await page.WaitForFunctionAsync("() => window.heldCommands.length === 1");

        await DragAsync(250, 170, 60, 40, async () =>
        {
            await page.EvaluateAsync("() => window.heldCommands[0]()").ConfigureAwait(true);
            await Expect(star1).ToHaveCountAsync(0).ConfigureAwait(true);
            await page.Mouse.MoveAsync((float)left + 330, (float)top + 230, new() { Steps = 5 }).ConfigureAwait(true);
            await Expect(cover).ToBeHiddenAsync().ConfigureAwait(true);
        });

        await Expect(cover).ToBeHiddenAsync();
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ALinkWhoseEndIsDraggedStaysHiddenDuringTheDrag(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var link = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1']");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 200, (float)top + 170);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-end='target'][data-id='pin-star1']")).ToHaveCountAsync(1);

        await DragAsync(226, 170, 104, 20, async () =>
        {
            await Expect(link).ToHaveAttributeAsync("data-reconnecting", string.Empty).ConfigureAwait(true);
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(true);
        });

        await Expect(link).Not.ToHaveAttributeAsync("data-reconnecting", string.Empty);
    }

    [Fact]
    public async Task AMoveThatARejectionStopsShowsNoArrowsUntilTheRelease()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var arrows = opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-arrow");
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

        await DragAsync(380, 190, 60, 40, async () =>
        {
            await page.EvaluateAsync("() => window.heldReconnects[0]()").ConfigureAwait(true);
            await page.WaitForFunctionAsync("() => window.reconnectsDone === 1").ConfigureAwait(true);
            await page.Mouse.MoveAsync((float)left + 250, (float)top + 300, new() { Steps = 5 }).ConfigureAwait(true);
            await page.WaitForTimeoutAsync(400).ConfigureAwait(true);
            await Expect(arrows).ToHaveCountAsync(0).ConfigureAwait(true);
        });
    }

    [Fact]
    public async Task AnotherDiagramDropsTheMovesThatWaitForTheirAnswer()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await page.EvaluateAsync("() => { const table = window.diagramController.table; window.rawTable = { version: table.version, boxes: [...table.boxes.values()], links: [...table.links.values()] }; }");
        await HoldTheFirstMoveAsync();
        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.heldMoves.length === 1");
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");

        await page.EvaluateAsync("() => window.diagramController.setTable(window.rawTable, true)");

        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
    }

    [Fact]
    public async Task ConnectingAndBoxSelectionFindTheElementsWithOneLookUp()
    {
        opened = await OpenWithTheControllerAsync("er");

        var counts = await opened.Page.EvaluateAsync<int[]>(
            """
            () => {
                const c = window.diagramController;
                const targets = [...c.table.boxes.values()].flatMap((box) => (box.ports ?? []).filter((port) => port.hidden).map((port) => ({ box, port })));
                const drag = { targets };
                window.idQueries = 0;
                c.revealPorts(drag);
                const portQueries = window.idQueries;
                for (const element of drag.revealed) {
                    element.removeAttribute('data-revealed');
                }
                let selected = [];
                c.setSelection = (ids) => { selected = ids; };
                window.idQueries = 0;
                c.finishBoxSelection({ startWorld: { x: -1e9, y: -1e9 }, world: { x: 1e9, y: 1e9 }, base: new Set() });
                delete c.setSelection;
                const links = [...c.table.links.values()].filter((link) => link.selectable).map((link) => link.id);
                return [targets.length, drag.revealed.length, portQueries, window.idQueries, links.length, links.filter((id) => selected.includes(id)).length];
            }
            """);

        Assert.NotEqual(0, counts[0]);
        Assert.NotEqual(0, counts[4]);
        Assert.Equal((counts[0], 0, 0, counts[4]), (counts[1], counts[2], counts[3], counts[5]));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AnEnterThatEndsAWordOfTheImeInThePickerPlacesNothing(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var picker = opened.View.Locator(".mussel-diagram-picker");
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var count = await nodes.CountAsync();
        await opened.View.FocusAsync();
        await opened.Page.Keyboard.PressAsync("Control+Enter");
        await Expect(picker).ToBeVisibleAsync();

        await opened.Page.EvaluateAsync(
            """
            () => {
                const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
                Object.defineProperty(event, 'keyCode', { get: () => 229 });
                document.querySelector('.mussel-diagram-picker-search').dispatchEvent(event);
            }
            """);

        await Expect(picker).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("Escape");
        await Expect(picker).ToHaveCountAsync(0);
        await Expect(nodes).ToHaveCountAsync(count);
    }

    [Fact]
    public async Task NewZoomLimitsBringTheViewBackAroundItsCenter()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);

        var view = await opened.Page.EvaluateAsync<double[]>(
            """
            () => {
                const c = window.diagramController;
                c.setView(4, 300, 200);
                c.setOptions({ ...c.options, maxZoom: 2 });
                const center = c.toWorld(c.centerPoint());
                return [c.view.zoom, center.x, center.y];
            }
            """);

        Assert.Equal(2, view[0]);
        Assert.Equal(300, view[1], 6);
        Assert.Equal(200, view[2], 6);
    }

    [Fact]
    public async Task QuickAddWaitsUntilTheDragEnds()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var picker = opened.View.Locator(".mussel-diagram-picker");

        await DragAsync(215, 140, 85, 205, async () =>
        {
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-box-selection")).ToHaveCountAsync(1).ConfigureAwait(true);
            await opened.Page.EvaluateAsync("() => window.diagramController.quickAdd()").ConfigureAwait(true);
            await Expect(picker).ToHaveCountAsync(0).ConfigureAwait(true);
        });

        await Expect(picker).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ACommitWithoutAnAnswerGoesBackAfterTheLimitAndTheNextOnesStayInStep()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var start = await star1.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await page.EvaluateAsync(
            """
            () => {
                window.diagramController.answerLimit = 1000;
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.lost = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if ((name === 'CommitMoveAsync') && (window.lost === 0)) {
                        window.lost++;
                        return new Promise(() => { });
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """);

        await DragAsync(250, 170, 60, 0, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", start);
        await Expect(star1).ToHaveAttributeAsync("transform", start);
        await DragAsync(250, 170, 60, 0, null);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", start);
        await page.WaitForFunctionAsync("() => window.diagramController.pending.length === 0");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 700, (float)top + 470);
        await Expect(star1).Not.ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Keyboard.PressAsync("Control+Z");
        await Expect(star1).ToHaveAttributeAsync("transform", start);
        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Keyboard.PressAsync("Delete");

        await Expect(star1).ToHaveCountAsync(0);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.lost"));
    }

    [Fact]
    public async Task KeysAfterACommandWithoutAnAnswerRunAfterTheLimit()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var (left, top) = await OriginAsync();
        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.EvaluateAsync(
            """
            () => {
                window.diagramController.answerLimit = 1000;
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.lost = 0;
                proto.invokeMethodAsync = function (name, ...args) {
                    if ((name === 'ExecuteCommandAsync') && (window.lost === 0)) {
                        window.lost++;
                        return new Promise(() => { });
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """);

        await page.Keyboard.PressAsync("Control+C");
        await page.WaitForFunctionAsync("() => window.lost === 1");
        await page.Keyboard.PressAsync("Delete");

        await Expect(rect).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ARejectionThatArrivesAfterTheLimitStillCountsSoTheNextCommitIsTaken()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var pin = opened.View.Locator("g.mussel-diagram-node[data-id='pin']");
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var start = await star1.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await page.EvaluateAsync(
            """
            () => {
                window.diagramController.answerLimit = 500;
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.late = 0;
                window.lateDone = false;
                proto.invokeMethodAsync = function (name, ...args) {
                    const result = invoke.call(this, name, ...args);
                    if ((name !== 'CommitMoveAsync') || (window.late > 0)) {
                        return result;
                    }
                    window.late++;
                    return new Promise((resolve) => setTimeout(() => {
                        resolve(result);
                        setTimeout(() => { window.lateDone = true; }, 0);
                    }, 2000));
                };
            }
            """);
        var box = await pin.Locator("use").BoundingBoxAsync() ?? throw new InvalidOperationException("The node is not shown.");
        await page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + (box.Width / 2) + 80, box.Y + (box.Height / 2), new() { Steps = 8 });
        await page.Mouse.UpAsync();
        await Expect(pin).ToHaveAttributeAsync("transform", "translate(46,156)");
        await page.WaitForFunctionAsync("() => window.lateDone");

        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.diagramController.pending.length === 0");

        await Expect(star1).Not.ToHaveAttributeAsync("transform", start);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.late"));
    }

    [Fact]
    public async Task TheFirstCommitAfterOneWithoutAnAnswerIsTakenBeforeTheLateRejectionArrives()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var pin = opened.View.Locator("g.mussel-diagram-node[data-id='pin']");
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var start = await star1.GetAttributeAsync("transform") ?? throw new InvalidOperationException("The node has no position.");
        await page.EvaluateAsync(
            """
            () => {
                window.diagramController.answerLimit = 500;
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.late = 0;
                window.lateDone = false;
                proto.invokeMethodAsync = function (name, ...args) {
                    const result = invoke.call(this, name, ...args);
                    if ((name !== 'CommitMoveAsync') || (window.late > 0)) {
                        return result;
                    }
                    window.late++;
                    return new Promise((resolve) => setTimeout(() => {
                        resolve(result);
                        setTimeout(() => { window.lateDone = true; }, 0);
                    }, 5000));
                };
            }
            """);
        var box = await pin.Locator("use").BoundingBoxAsync() ?? throw new InvalidOperationException("The node is not shown.");
        await page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + (box.Width / 2) + 80, box.Y + (box.Height / 2), new() { Steps = 8 });
        await page.Mouse.UpAsync();
        await Expect(pin).ToHaveAttributeAsync("transform", "translate(46,156)");
        await page.WaitForFunctionAsync("() => window.diagramController.pending.length === 0");

        await DragAsync(250, 170, 60, 0, null);
        await page.WaitForFunctionAsync("() => window.diagramController.pending.length === 0");

        await Expect(star1).Not.ToHaveAttributeAsync("transform", start, new() { Timeout = 1000 });
        Assert.False(await page.EvaluateAsync<bool>("() => window.lateDone"));
    }

    [Fact]
    public async Task ASelectionChangeWithoutAnAnswerIsNotAwaitedAfterTheLimit()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        await page.EvaluateAsync(
            """
            () => {
                window.diagramController.answerLimit = 1000;
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                proto.invokeMethodAsync = function (name, ...args) {
                    const result = invoke.call(this, name, ...args);
                    return name === 'OnSelectionChangedAsync' ? new Promise(() => { }) : result;
                };
            }
            """);
        var (left, top) = await OriginAsync();

        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        var waiting = await page.EvaluateAsync<bool>("() => window.diagramController.busy()");

        await page.WaitForFunctionAsync("() => !window.diagramController.busy()", null, new() { Timeout = 5000 });
        Assert.True(waiting);
    }

    [Fact]
    public async Task KeysDoNotWaitForASelectionChangeWithoutAnAnswer()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                proto.invokeMethodAsync = function (name, ...args) {
                    const result = invoke.call(this, name, ...args);
                    return name === 'OnSelectionChangedAsync' ? new Promise(() => { }) : result;
                };
            }
            """);
        var (left, top) = await OriginAsync();

        await page.Mouse.ClickAsync((float)left + 380, (float)top + 190);
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Keyboard.PressAsync("Delete");

        await Expect(rect).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ThePickerSearchedInCSharpSendsOnlyTheWordsAndKeepsTheOrderOfTheScript()
    {
        opened = await OpenWithTheControllerAsync(string.Empty);
        var page = opened.Page;
        var picker = opened.View.Locator(".mussel-diagram-picker");
        var search = picker.Locator(".mussel-diagram-picker-search");
        var items = picker.Locator(".mussel-diagram-picker-item");
        await opened.View.FocusAsync();
        await page.Keyboard.PressAsync("Control+Enter");
        await Expect(picker).ToBeVisibleAsync();
        await search.FillAsync("グループ");
        var inScript = await items.EvaluateAllAsync<string[]>("(xs) => xs.map((x) => x.getAttribute('data-type'))");
        await search.FillAsync(string.Empty);
        await Expect(items).Not.ToHaveCountAsync(inScript.Length);
        await page.EvaluateAsync(
            """
            () => {
                const c = window.diagramController;
                const setOptions = c.setOptions;
                c.setOptions = function (options) {
                    return setOptions.call(this, { ...options, hostSearch: true });
                };
                c.setOptions(c.options);
                const invoke = c.dotnet.invokeMethodAsync;
                window.searches = [];
                c.dotnet.invokeMethodAsync = function (name, ...args) {
                    if (name === 'SearchTypesAsync') {
                        window.searches.push(args);
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """);

        await search.FillAsync("グループ");

        await Expect(items).ToHaveCountAsync(inScript.Length);
        Assert.Equal(["group", "frame", "dashed"], inScript);
        Assert.Equal(inScript, await items.EvaluateAllAsync<string[]>("(xs) => xs.map((x) => x.getAttribute('data-type'))"));
        Assert.Equal("[[\"グループ\"]]", await page.EvaluateAsync<string>("() => JSON.stringify(window.searches)"));
    }

    [Fact]
    public async Task ABoxSelectionThatPansPastTheDrawnAreaSelectsTheLinksBetweenTheBoxesInside()
    {
        opened = await OpenWithTheControllerAsync("large");
        var page = opened.Page;
        await opened.View.FocusAsync();
        await page.Keyboard.PressAsync("Control+0");
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\)$"));
        var start = await page.EvaluateAsync<double[]>(
            """
            () => {
                const c = window.diagramController;
                const finish = c.finishBoxSelection;
                c.finishBoxSelection = function (drag) {
                    const rect = this.selectionRect(drag);
                    const drawn = new Set([...this.canvas.querySelectorAll('g.mussel-diagram-link[data-id]')].map((x) => x.getAttribute('data-id')));
                    const inside = (b) => b && (b.x >= rect.x) && (b.y >= rect.y) && (b.x + b.w <= rect.x + rect.w) && (b.y + b.h <= rect.y + rect.h);
                    window.undrawn = [...this.table.links.values()]
                        .filter((link) => link.selectable && !drawn.has(link.id) && inside(this.table.boxes.get(link.source)) && inside(this.table.boxes.get(link.target)))
                        .map((link) => link.id);
                    return finish.call(this, drag);
                };
                const view = c.root.getBoundingClientRect();
                for (let y = view.top + 60; y < view.top + 300; y += 10) {
                    for (let x = view.left + 60; x < view.left + 300; x += 10) {
                        const target = document.elementFromPoint(x, y);
                        if (c.canvas.contains(target) && !target.closest('.mussel-diagram-node, .mussel-diagram-group, .mussel-diagram-link, .mussel-diagram-label')) {
                            window.startView = { ...c.view };
                            return [x, y, view.right - 8, view.bottom - 8];
                        }
                    }
                }
                return [];
            }
            """);
        Assert.Equal(4, start.Length);

        await page.Mouse.MoveAsync((float)start[0], (float)start[1]);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)start[2], (float)start[3], new() { Steps = 10 });
        await page.WaitForFunctionAsync("() => (window.diagramController.view.x < window.startView.x - 1500) && (window.diagramController.view.y < window.startView.y - 900)");
        await page.Mouse.UpAsync();

        var undrawn = await page.EvaluateAsync<string[]>("() => window.undrawn");
        var selected = await page.EvaluateAsync<string[]>("() => [...window.diagramController.selection]");
        Assert.NotEmpty(undrawn);
        Assert.All(undrawn, id => Assert.Contains(id, selected));
    }

    private async Task HoldTheFirstMoveAsync() =>
        await opened!.Page.EvaluateAsync(
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
            """).ConfigureAwait(true);

    private Task<SamplePage> OpenWithFramedMovesAsync() =>
        OpenWithScriptArgumentsChangedAsync("(item) => { if ('livePreviewLimit' in item) { item.livePreviewLimit = 1; } }");

    private Task<SamplePage> OpenWithScriptArgumentsChangedAsync(string change) =>
        OpenWithInitScriptAsync(
            $$"""
            (() => {
                const change = {{change}};
                const parse = JSON.parse;
                JSON.parse = function (text, reviver) {
                    const value = parse.call(this, text, reviver);
                    for (const item of Array.isArray(value) ? value : []) {
                        if (item && (typeof item === 'object')) {
                            change(item);
                        }
                    }
                    return value;
                };
            })();
            """);

    private Task<SamplePage> OpenHoldingTheSelectionsOfCSharpAsync() =>
        OpenWithInitScriptAsync(
            """
            (() => {
                window.holdSelects = false;
                window.heldSelects = [];
                const set = WeakMap.prototype.set;
                WeakMap.prototype.set = function (key, value) {
                    if ((key instanceof Element) && key.classList.contains('mussel-diagram') && value && (typeof value.select === 'function') && !Object.hasOwn(value, 'select')) {
                        const select = value.select;
                        value.select = function (...args) {
                            if (!window.holdSelects) {
                                return select.apply(this, args);
                            }
                            window.heldSelects.push(() => select.apply(this, args));
                            return undefined;
                        };
                    }
                    return set.call(this, key, value);
                };
            })();
            """);

    private Task<SamplePage> OpenWithTheControllerAsync(string path) =>
        OpenWithInitScriptAsync(
            """
            (() => {
                const set = WeakMap.prototype.set;
                WeakMap.prototype.set = function (key, value) {
                    if ((key instanceof Element) && key.classList.contains('mussel-diagram') && value && (typeof value.setTable === 'function')) {
                        window.diagramController = value;
                    }
                    return set.call(this, key, value);
                };
                const query = Element.prototype.querySelector;
                window.idQueries = 0;
                Element.prototype.querySelector = function (selector) {
                    if ((typeof selector === 'string') && selector.startsWith('[data-id=')) {
                        window.idQueries++;
                    }
                    return query.call(this, selector);
                };
            })();
            """,
            path);

    private async Task<SamplePage> OpenWithInitScriptAsync(string script, string path = "")
    {
        var page = await app.OpenBlankAsync().ConfigureAwait(true);
        await page.Page.AddInitScriptAsync(script).ConfigureAwait(true);
        await page.Page.GotoAsync($"{app.BaseAddress}render-mode/server?redirect={Uri.EscapeDataString($"/{path}")}").ConfigureAwait(true);
        await page.WaitForDiagramAsync().ConfigureAwait(true);
        return page;
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
