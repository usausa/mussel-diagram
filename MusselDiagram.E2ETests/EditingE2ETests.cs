namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class EditingE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
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
    public async Task PaletteItemsArePlacedByDragging(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await Expect(nodes).ToHaveCountAsync(9);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);

        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='星 3']")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task APlacedNodeLeavesASelectionMadeBeforeTheAnswer()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        await Expect(nodes).ToHaveCountAsync(9);
        await page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldDrops = [];
                window.selected = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name === 'CommitDropAsync') {
                        window.view = this;
                        return new Promise((resolve) => window.heldDrops.push(() => resolve(invoke.call(this, name, ...args))));
                    }
                    const result = invoke.call(this, name, ...args);
                    if (name === 'OnSelectionChangedAsync') {
                        result.then(() => window.selected.push(JSON.stringify(args[0])), () => { });
                    }
                    return result;
                };
            }
            """);

        await DragInAsync(page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);
        await page.WaitForFunctionAsync("() => window.heldDrops.length === 1");
        await rect.ClickAsync();
        await page.WaitForFunctionAsync("() => window.selected.includes(JSON.stringify(['rect']))");
        await page.EvaluateAsync("() => window.heldDrops[0]()");
        await Expect(nodes).ToHaveCountAsync(10);
        await page.EvaluateAsync("async () => { await window.view.invokeMethodAsync('SearchTypesAsync', '', []); await new Promise((resolve) => setTimeout(resolve, 0)); }");

        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task TheLaterOfTwoPlacedNodesIsSelectedWhenTheFirstArrivesBeforeItsAnswer()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var star = page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']");
        await Expect(nodes).ToHaveCountAsync(9);
        await HoldDropsAsync();
        await DragInAsync(star, 120, 500);
        await DragInAsync(star, 300, 520);
        await page.WaitForFunctionAsync("() => window.heldDrops.length === 1");

        await page.EvaluateAsync("() => window.heldDrops[0]()");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='星 3']")).ToHaveCountAsync(1);
        await page.WaitForFunctionAsync("() => window.heldDrops.length === 2");
        await page.EvaluateAsync("() => window.heldDrops[1]()");

        await Expect(nodes).ToHaveCountAsync(11);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='星 4']")).ToHaveCountAsync(1);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task APlacedNodeLeavesASelectionThatCSharpMadeBeforeTheAnswer()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var star1 = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await Expect(nodes).ToHaveCountAsync(9);
        var (left, top) = await OriginAsync();
        await page.Mouse.MoveAsync((float)left + 250, (float)top + 170);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)left + 310, (float)top + 170, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");
        await ClickAsync(700, 470);
        await Expect(star1).Not.ToHaveAttributeAsync("data-selected", string.Empty);
        await HoldDropsAsync();
        await DragInAsync(page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);
        await page.WaitForFunctionAsync("() => window.heldDrops.length === 1");

        await page.EvaluateAsync("() => window.view.invokeMethodAsync('ExecuteCommandAsync', 'Undo', null)");
        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.EvaluateAsync("() => window.heldDrops[0]()");
        await Expect(nodes).ToHaveCountAsync(10);
        await page.EvaluateAsync("async () => { await window.view.invokeMethodAsync('SearchTypesAsync', '', []); await new Promise((resolve) => setTimeout(resolve, 0)); }");

        await Expect(star1).ToHaveAttributeAsync("data-selected", string.Empty);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task APlacedNodeThatArrivesLaterLeavesTheFocusWhereItIs()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var search = page.Locator(".mussel-diagram-palette-search");
        await HoldDropsAsync();
        await DragInAsync(page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);
        await page.WaitForFunctionAsync("() => window.heldDrops.length === 1");
        await search.FocusAsync();

        await page.EvaluateAsync("() => window.heldDrops[0]()");

        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='星 3']")).ToHaveCountAsync(1);
        await Expect(search).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ThePaletteDragShowsTheColorsOfTheItem()
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var item = page.Locator(".mussel-diagram-palette-item[data-mussel-type='rectangle']");
        var from = await item.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(from.X + (from.Width / 2) + 20, from.Y + (from.Height / 2) + 5, new() { Steps = 4 });
        await Expect(page.Locator(".mussel-diagram-drag-ghost .mussel-diagram-palette-shape")).ToHaveCountAsync(1);

        var colors = await page.EvaluateAsync<string[]>(
            """
            () => {
                const item = getComputedStyle(document.querySelector(".mussel-diagram-palette-item[data-mussel-type='rectangle'] .mussel-diagram-palette-shape"));
                const ghost = getComputedStyle(document.querySelector('.mussel-diagram-drag-ghost .mussel-diagram-palette-shape'));
                return [item.fill, ghost.fill, item.stroke, ghost.stroke];
            }
            """);
        await page.Mouse.UpAsync();

        Assert.Equal(colors[0], colors[1]);
        Assert.Equal(colors[2], colors[3]);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task DragSourcesGiveTheirValue(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-drag-source"), 300, 500);

        await Expect(opened.View.Locator("g.mussel-diagram-node > text.mussel-diagram-label[data-label='外から置いた星']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task DroppingANodeOnALinkSplicesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(7);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 130, 180);

        await Expect(links).ToHaveCountAsync(8);
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PaletteDragsScrollOnlyFromInsideTheView(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        var source = page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']");
        var from = await source.BoundingBoxAsync() ?? throw new InvalidOperationException("The source is not shown.");
        var view = await opened.View.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");

        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(view.X + 10, view.Y + 500, new() { Steps = 8 });
        await page.WaitForTimeoutAsync(300);
        await Expect(viewport).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");

        await page.Mouse.MoveAsync(view.X + 300, view.Y + 500, new() { Steps = 4 });
        await page.Mouse.MoveAsync(view.X + 10, view.Y + 500, new() { Steps = 4 });
        await Expect(viewport).Not.ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        await page.Mouse.UpAsync();
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task MovingALonelyNodeOntoALinkSplicesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(1);

        var page = opened.Page;
        var (left, top) = await OriginAsync();
        await page.Mouse.MoveAsync((float)left + 120, (float)top + 500);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)left + 130, (float)top + 180, new() { Steps = 12 });
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-split-target]")).ToHaveCountAsync(1);
        await page.Mouse.UpAsync();

        await Expect(links).ToHaveCountAsync(8);
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-split-target]")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TryingASpliceKeepsTheOrderOfOverlappingNodes(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var selected = opened.View.Locator("g.mussel-diagram-node[data-selected]");
        var star = page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']");
        await DragInAsync(star, 120, 500);
        await Expect(selected).ToHaveCountAsync(1);
        var back = await selected.GetAttributeAsync("data-id");
        await DragInAsync(star, 150, 500);
        await Expect(selected).Not.ToHaveAttributeAsync("data-id", back!);
        var front = await selected.GetAttributeAsync("data-id");

        var (left, top) = await OriginAsync();
        await page.Mouse.MoveAsync((float)left + 104, (float)top + 500);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)left + 130, (float)top + 180, new() { Steps = 12 });
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-split-target]")).ToHaveCountAsync(1);
        await page.Mouse.MoveAsync((float)left + 104, (float)top + 500, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        await page.Mouse.MoveAsync((float)left + 400, (float)top + 650);
        await page.Mouse.MoveAsync((float)left + 135, (float)top + 500);

        await Expect(opened.View.Locator($".mussel-diagram-arrow[data-id='{front}']")).Not.ToHaveCountAsync(0);
        await Expect(opened.View.Locator($".mussel-diagram-arrow[data-id='{back}']")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task WaitModeKeepsTheEditorUntilTheAnswer(string mode)
    {
        opened = await app.OpenAsync("?wait=true", mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.TypeAsync("Star B");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='Star B']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task KeysDoNotRunWhileThePointerPans(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var selected = opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]");
        await ClickAsync(250, 170);
        await Expect(selected).ToHaveCountAsync(1);

        var (left, top) = await OriginAsync();
        await page.Mouse.MoveAsync((float)left + 700, (float)top + 470);
        await page.Mouse.DownAsync(new() { Button = MouseButton.Middle });
        await page.Mouse.MoveAsync((float)left + 650, (float)top + 450, new() { Steps = 4 });
        await page.Keyboard.PressAsync("Delete");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.MoveAsync((float)left + 600, (float)top + 430, new() { Steps = 4 });
        await page.Mouse.UpAsync(new() { Button = MouseButton.Middle });

        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(-50,-20) scale(1)");
        await Expect(nodes).ToHaveCountAsync(9);
        await Expect(selected).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task KeysDoNotRunWhileAPaletteItemIsDragged(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var selected = opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]");
        var source = page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']");
        await ClickAsync(250, 170);
        await Expect(selected).ToHaveCountAsync(1);
        var from = await source.BoundingBoxAsync() ?? throw new InvalidOperationException("The source is not shown.");
        var (left, top) = await OriginAsync();

        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)left + 120, (float)top + 500, new() { Steps = 12 });
        await page.Keyboard.PressAsync("Delete");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();

        await Expect(nodes).ToHaveCountAsync(9);
        await Expect(selected).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task KeysCopyPasteDeleteAndUndo(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var keyboard = opened.Page.Keyboard;
        await ClickAsync(250, 170);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]")).ToHaveCountAsync(1);

        await keyboard.PressAsync("Control+C");
        await keyboard.PressAsync("Control+V");
        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]:not([data-id='star1'])")).ToHaveCountAsync(1);

        await keyboard.PressAsync("Delete");
        await Expect(nodes).ToHaveCountAsync(9);
        await keyboard.PressAsync("Control+Z");
        await Expect(nodes).ToHaveCountAsync(10);
        await keyboard.PressAsync("Control+Y");
        await Expect(nodes).ToHaveCountAsync(9);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TextIsEditedInPlace(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToBeVisibleAsync();
        await Expect(editor).ToHaveValueAsync("星 1");
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.TypeAsync("Star A");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='Star A']")).ToHaveCountAsync(1);

        await ClickAsync(600, 230);
        await opened.Page.Keyboard.PressAsync("F2");
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.TypeAsync("x");
        await opened.Page.Keyboard.PressAsync("Escape");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='triangle'] > text.mussel-diagram-label[data-label='長いラベルは 2 行まで折り返します']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ClickingInTheEditorKeepsItOpen(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToHaveValueAsync("星 1");
        await editor.ClickAsync();
        await Expect(editor).ToBeFocusedAsync();
        await opened.Page.Keyboard.PressAsync("End");
        await opened.Page.Keyboard.TypeAsync("A");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='星 1A']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PanningWithTheMiddleButtonKeepsTheEditorOpen(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToHaveValueAsync("星 1");
        await opened.Page.Mouse.MoveAsync((float)left + 700, (float)top + 470);
        await opened.Page.Mouse.DownAsync(new() { Button = MouseButton.Middle });
        await opened.Page.Mouse.MoveAsync((float)left + 640, (float)top + 470, new() { Steps = 4 });
        await opened.Page.Mouse.UpAsync(new() { Button = MouseButton.Middle });

        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(-60,0) scale(1)");
        await Expect(editor).ToBeFocusedAsync();
        await opened.Page.Keyboard.PressAsync("End");
        await opened.Page.Keyboard.TypeAsync("B");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='星 1B']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task EditingKeysAndPaletteDragsKeepTheTypedText(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToHaveValueAsync("星 1");
        await opened.Page.Keyboard.PressAsync("End");
        await opened.Page.Keyboard.TypeAsync("E");
        await opened.Page.Keyboard.PressAsync("F2");

        await Expect(editor).ToHaveValueAsync("星 1E");
        await opened.Page.Keyboard.TypeAsync("F");
        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 300, 520);

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='星 1EF']")).ToHaveCountAsync(1);
        await Expect(opened.View.Locator("g.mussel-diagram-node")).ToHaveCountAsync(10);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PanningTheBackgroundKeepsTheEditorOpen(string mode)
    {
        opened = await app.OpenAsync("?backgroundpan=true", mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 250, (float)top + 204);
        await Expect(editor).ToHaveValueAsync("星 1");
        await opened.Page.Mouse.MoveAsync((float)left + 700, (float)top + 470);
        await opened.Page.Mouse.DownAsync();
        await opened.Page.Mouse.MoveAsync((float)left + 640, (float)top + 470, new() { Steps = 4 });
        await opened.Page.Mouse.UpAsync();

        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(-60,0) scale(1)");
        await Expect(editor).ToBeFocusedAsync();
        await opened.Page.Keyboard.PressAsync("End");
        await opened.Page.Keyboard.TypeAsync("C");
        await opened.Page.Mouse.ClickAsync((float)left + 700, (float)top + 470);

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] > text.mussel-diagram-label[data-label='星 1C']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TemplateTextIsEditedInPlace(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='card']"), 300, 520);

        var card = opened.View.Locator("g.mussel-diagram-node[data-type='card'][data-template]:not([data-id='card'])");
        var title = card.Locator("text[data-slot='title']");
        var note = card.Locator("text[data-slot='note']");
        await Expect(title).ToHaveTextAsync("新しいカード");
        await Expect(card.Locator(":scope > .mussel-diagram-label")).ToHaveCountAsync(0);
        await title.DblClickAsync();
        await Expect(editor).ToHaveValueAsync("新しいカード");
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.TypeAsync("Review");
        await opened.Page.Keyboard.PressAsync("Enter");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(title).ToHaveTextAsync("Review");

        await Expect(note).ToHaveTextAsync("補足");
        await note.DblClickAsync();
        await Expect(editor).ToHaveValueAsync(string.Empty);
        await Expect(editor).ToHaveAttributeAsync("placeholder", "補足");
        await opened.Page.Keyboard.TypeAsync("Sato");
        await opened.Page.Keyboard.PressAsync("Enter");
        await Expect(note).ToHaveTextAsync("Sato");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AnEmptiedTemplateTextShowsItsDefault(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='card']"), 300, 520);
        var note = opened.View.Locator("g.mussel-diagram-node[data-type='card'][data-template]:not([data-id='card'])").Locator("text[data-slot='note']");
        await note.DblClickAsync();
        await opened.Page.Keyboard.TypeAsync("Sato");
        await opened.Page.Keyboard.PressAsync("Enter");
        await Expect(note).ToHaveTextAsync("Sato");

        await note.DblClickAsync();
        await Expect(editor).ToHaveValueAsync("Sato");
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.PressAsync("Delete");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(note).ToHaveTextAsync("補足");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task GroupAndUngroupWithKeys(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var groups = opened.View.Locator("g.mussel-diagram-group");
        await Expect(groups).ToHaveCountAsync(4);
        await ClickAsync(250, 170);
        var (left, top) = await OriginAsync();
        await opened.Page.Keyboard.DownAsync("Shift");
        await opened.Page.Mouse.ClickAsync((float)left + 250, (float)top + 300);
        await opened.Page.Keyboard.UpAsync("Shift");

        await opened.Page.Keyboard.PressAsync("Control+G");
        await Expect(groups).ToHaveCountAsync(5);
        await Expect(opened.View.Locator("g.mussel-diagram-group[data-selected]")).ToHaveCountAsync(1);

        await opened.Page.Keyboard.PressAsync("Control+Shift+U");
        await Expect(groups).ToHaveCountAsync(4);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(2);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task QuickAddPlacesTheChosenType(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await ClickAsync(700, 470);

        await opened.Page.Keyboard.PressAsync("Control+Enter");
        var picker = opened.View.Locator(".mussel-diagram-picker");
        await Expect(picker).ToBeVisibleAsync();
        await opened.Page.Keyboard.TypeAsync("diamond");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-type='diamond'][data-selected]")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ThePressThatChoosesInTheQuickAddListEndsThere(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await ClickAsync(700, 470);
        await opened.Page.Keyboard.PressAsync("Control+Enter");
        await opened.Page.Keyboard.TypeAsync("diamond");
        var item = opened.View.Locator(".mussel-diagram-picker-item[data-type='diamond']");
        await Expect(item).ToBeVisibleAsync();
        var box = await item.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");

        await opened.Page.Mouse.MoveAsync(box.X + 10, box.Y + 10);
        await opened.Page.Mouse.DownAsync();
        await opened.Page.Mouse.MoveAsync(box.X - 60, box.Y - 60, new() { Steps = 4 });
        await Expect(opened.View.Locator("rect.mussel-diagram-box-selection")).ToHaveCountAsync(0);
        await opened.Page.Mouse.UpAsync();

        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-type='diamond'][data-selected]")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ArrowKeysInTheQuickAddListKeepTheChoiceInView(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        await ClickAsync(700, 470);
        await opened.Page.Keyboard.PressAsync("Control+Enter");
        var list = opened.View.Locator(".mussel-diagram-picker-list");
        await Expect(list).ToBeVisibleAsync();
        var count = await list.Locator(".mussel-diagram-picker-item").CountAsync();
        await list.EvaluateAsync("list => { window.firstOption = list.firstElementChild; }");

        for (var i = 1; i < count; i++)
        {
            await opened.Page.Keyboard.PressAsync("ArrowDown");
        }

        var shown = await list.EvaluateAsync<bool[]>(
            """
            list => {
                const box = list.getBoundingClientRect();
                const item = list.querySelector('[aria-selected="true"]').getBoundingClientRect();
                return [list.scrollHeight > list.clientHeight, (item.top >= box.top - 1) && (item.bottom <= box.bottom + 1), window.firstOption.isConnected];
            }
            """);
        Assert.Equal([true, true, true], shown);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TooltipsAndZoomKeysWork(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='triangle'] > title")).ToHaveTextAsync("三角: 長いラベルは 2 行まで折り返します");

        await ClickAsync(700, 470);
        await opened.Page.Keyboard.PressAsync("Control+Equal");

        await Expect(opened.View.Locator(".mussel-diagram-canvas > .mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\.2\)"));
        await opened.Page.Keyboard.PressAsync("Control+0");
        await Expect(opened.View.Locator(".mussel-diagram-canvas > .mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\)"));
    }

    private async Task HoldDropsAsync() =>
        await opened!.Page.EvaluateAsync(
            """
            () => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.heldDrops = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (name !== 'CommitDropAsync') {
                        return invoke.call(this, name, ...args);
                    }
                    window.view = this;
                    return new Promise((resolve) => window.heldDrops.push(() => resolve(invoke.call(this, name, ...args))));
                };
            }
            """).ConfigureAwait(true);

    private async Task<(double X, double Y)> OriginAsync()
    {
        var box = await opened!.View.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The view is not shown.");
        return (box.X, box.Y);
    }

    private async Task ClickAsync(float x, float y)
    {
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        await opened!.Page.Mouse.ClickAsync((float)left + x, (float)top + y).ConfigureAwait(true);
    }

    private async Task DragInAsync(ILocator source, float x, float y)
    {
        var page = opened!.Page;
        var from = await source.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The source is not shown.");
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2)).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync((float)left + x, (float)top + y, new() { Steps = 12 }).ConfigureAwait(true);
        await page.Mouse.UpAsync().ConfigureAwait(true);
    }
}
