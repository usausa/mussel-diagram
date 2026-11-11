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
        await Expect(nodes).ToHaveCountAsync(6);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='server']"), 120, 500);

        await Expect(nodes).ToHaveCountAsync(7);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected] > text.mussel-diagram-label[data-label='Server 3']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task DragSourcesGiveTheirValue(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-drag-source"), 300, 500);

        await Expect(opened.View.Locator("g.mussel-diagram-node > text.mussel-diagram-label[data-label='Imported server']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task DroppingANodeOnALinkSplicesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(5);

        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='server']"), 130, 184);

        await Expect(links).ToHaveCountAsync(6);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task MovingALonelyNodeOntoALinkSplicesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await DragInAsync(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='server']"), 120, 500);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]")).ToHaveCountAsync(1);

        var page = opened.Page;
        var (left, top) = await OriginAsync();
        await page.Mouse.MoveAsync((float)left + 120, (float)top + 500);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)left + 130, (float)top + 184, new() { Steps = 12 });
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-split-target]")).ToHaveCountAsync(1);
        await page.Mouse.UpAsync();

        await Expect(links).ToHaveCountAsync(6);
        await Expect(opened.View.Locator("g.mussel-diagram-link[data-split-target]")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task WaitModeKeepsTheEditorUntilTheAnswer(string mode)
    {
        opened = await app.OpenAsync("?wait=true", mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 254, (float)top + 208);
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.TypeAsync("Web B");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='web1'] > text.mussel-diagram-label[data-label='Web B']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task KeysCopyPasteDeleteAndUndo(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var keyboard = opened.Page.Keyboard;
        await ClickAsync(254, 174);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='web1'][data-selected]")).ToHaveCountAsync(1);

        await keyboard.PressAsync("Control+C");
        await keyboard.PressAsync("Control+V");
        await Expect(nodes).ToHaveCountAsync(7);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-selected]:not([data-id='web1'])")).ToHaveCountAsync(1);

        await keyboard.PressAsync("Delete");
        await Expect(nodes).ToHaveCountAsync(6);
        await keyboard.PressAsync("Control+Z");
        await Expect(nodes).ToHaveCountAsync(7);
        await keyboard.PressAsync("Control+Y");
        await Expect(nodes).ToHaveCountAsync(6);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TextIsEditedInPlace(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var editor = opened.View.Locator("textarea.mussel-diagram-text-editor");
        var (left, top) = await OriginAsync();

        await opened.Page.Mouse.DblClickAsync((float)left + 254, (float)top + 208);
        await Expect(editor).ToBeVisibleAsync();
        await Expect(editor).ToHaveValueAsync("Web server 1");
        await opened.Page.Keyboard.PressAsync("Control+A");
        await opened.Page.Keyboard.TypeAsync("Web A");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='web1'] > text.mussel-diagram-label[data-label='Web A']")).ToHaveCountAsync(1);

        await ClickAsync(604, 234);
        await opened.Page.Keyboard.PressAsync("F2");
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.TypeAsync("x");
        await opened.Page.Keyboard.PressAsync("Escape");
        await Expect(editor).ToHaveCountAsync(0);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='db'] > text.mussel-diagram-label[data-label='Amazon RDS primary database']")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task GroupAndUngroupWithKeys(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var groups = opened.View.Locator("g.mussel-diagram-group");
        await Expect(groups).ToHaveCountAsync(4);
        await ClickAsync(254, 174);
        var (left, top) = await OriginAsync();
        await opened.Page.Keyboard.DownAsync("Shift");
        await opened.Page.Mouse.ClickAsync((float)left + 254, (float)top + 304);
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
        await opened.Page.Keyboard.TypeAsync("decision");
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(nodes).ToHaveCountAsync(7);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-type='decision'][data-selected]")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TooltipsAndZoomKeysWork(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='db'] > title")).ToHaveTextAsync("Database: Amazon RDS primary database");

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
        var from = await source.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The source is not shown.");
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2)).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync((float)left + x, (float)top + y, new() { Steps = 12 }).ConfigureAwait(true);
        await page.Mouse.UpAsync().ConfigureAwait(true);
    }
}
