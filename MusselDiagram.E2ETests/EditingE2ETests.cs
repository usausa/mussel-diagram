namespace MusselDiagram.E2ETests;

using static Microsoft.Playwright.Assertions;

public sealed class EditingE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private DiagramPage? opened;

    public static TheoryData<string> ModeNames => ["server", "wasm", "auto"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task PaletteItemsArePlacedByDragging(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await Expect(nodes).ToHaveCountAsync(9);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']"), 120, 500);

        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='星 3']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task DragSourcesGiveTheirValue(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-drag-source"), 300, 500);

        await Expect(opened.View.Locator("g.mussel-diagram-node > text.mussel-diagram-label[data-label='外から置いた星']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
    public async Task PaletteDragsScrollOnlyFromInsideTheView(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        var source = page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']");
        await page.Locator(".mussel-diagram-palette[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
    public async Task TooltipsAndZoomKeysWork(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='triangle'] > title")).ToHaveTextAsync("三角: 長いラベルは 2 行まで折り返します");

        await ClickAsync(700, 470);
        await opened.Page.Keyboard.PressAsync("Control+Equal");

        await Expect(opened.View.Locator(".mussel-diagram-canvas > .mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new System.Text.RegularExpressions.Regex(@"scale\(1\.2\)"));
        await opened.Page.Keyboard.PressAsync("Control+0");
        await Expect(opened.View.Locator(".mussel-diagram-canvas > .mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new System.Text.RegularExpressions.Regex(@"scale\(1\)"));
    }

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

    private async Task DragInAsync(Microsoft.Playwright.ILocator source, float x, float y)
    {
        var page = opened!.Page;
        await source.Locator("xpath=ancestor-or-self::*[@data-attached][1]").WaitForAsync(new() { State = WaitForSelectorState.Attached }).ConfigureAwait(true);
        var from = await source.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The source is not shown.");
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2)).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync((float)left + x, (float)top + y, new() { Steps = 12 }).ConfigureAwait(true);
        await page.Mouse.UpAsync().ConfigureAwait(true);
    }
}
