namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ConformanceE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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
    [MemberData(nameof(ModeNames))]
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

    private async Task<(double X, double Y)> OriginAsync()
    {
        var box = await opened!.View.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The view is not shown.");
        return (box.X, box.Y);
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
