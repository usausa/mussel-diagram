namespace MusselDiagram.E2ETests;

using static Microsoft.Playwright.Assertions;

public sealed class OperationE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
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
    public async Task SnappingAlignsEdgesAndShowsGuides(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");

        await DragAsync(250, 170, 60, 40, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay line.mussel-diagram-guide").First).ToBeAttachedAsync().ConfigureAwait(true));

        await Expect(node).ToHaveAttributeAsync("transform", "translate(282,190)");
        await Expect(opened.View.Locator(".mussel-diagram-overlay line.mussel-diagram-guide")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheGridTakesTheCenterOfANode(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");

        await DragAsync(250, 170, 427, 337, false, null);

        await Expect(node).ToHaveAttributeAsync("transform", "translate(656,486)");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ResizingANodeChangesItsBounds(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var shape = opened.View.Locator("g.mussel-diagram-node[data-id='rect'] > rect.mussel-diagram-shape");
        await ClickAsync(380, 190);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-handle]")).ToHaveCountAsync(8);

        await DragAsync(430, 220, 30, 70, false, null);

        await Expect(shape).ToHaveAttributeAsync("width", "130");
        await Expect(shape).ToHaveAttributeAsync("height", "130");
        await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-resize-frame")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ConnectingFromAPortAddsALink(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(7);

        await DragAsync(430, 190, 170, 40, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveCountAsync(1).ConfigureAwait(true));

        await Expect(links).ToHaveCountAsync(8);
        await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ReleasingALinkOnEmptySpaceOpensTheTypePicker(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var picker = opened.View.Locator(".mussel-diagram-picker");
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await Expect(nodes).ToHaveCountAsync(9);

        await DragAsync(430, 190, 270, 330, false, null);
        await Expect(picker).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("Escape");
        await Expect(picker).ToHaveCountAsync(0);

        await DragAsync(430, 190, 270, 330, false, null);
        await opened.Page.Keyboard.TypeAsync("tri");
        await Expect(picker.Locator(".mussel-diagram-picker-item")).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(nodes).ToHaveCountAsync(10);
        await Expect(opened.View.Locator("g.mussel-diagram-link")).ToHaveCountAsync(8);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-type='triangle']")).ToHaveCountAsync(2);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ReconnectingALinkEndMovesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var hit = opened.View.Locator("g.mussel-diagram-link[data-id='pin-star1'] > path.mussel-diagram-link-hit");
        await ClickAsync(200, 170);
        var handle = opened.View.Locator(".mussel-diagram-overlay [data-end='target'][data-id='pin-star1']");
        await Expect(handle).ToHaveCountAsync(1);

        await DragAsync(226, 170, 374, 60, false, null);

        await Expect(hit).ToHaveAttributeAsync("d", new System.Text.RegularExpressions.Regex("L576,230$"));
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task BoxSelectionSelectsTheElementsInside(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        await DragAsync(215, 140, 85, 205, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-box-selection")).ToHaveCountAsync(1).ConfigureAwait(true));

        await Expect(opened.View.Locator(".mussel-diagram-overlay rect.mussel-diagram-selection")).ToHaveCountAsync(2);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star2']")).ToHaveAttributeAsync("data-selected", string.Empty);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task MovingANodeIntoAGroupChangesItsParent(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");

        await DragAsync(380, 190, 200, 40, true, async () =>
            await Expect(opened.View.Locator("g.mussel-diagram-group[data-id='right']")).ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true));
        await Expect(rect).ToHaveAttributeAsync("transform", "translate(530,200)");

        var label = opened.View.Locator("g.mussel-diagram-group[data-id='right'] > text.mussel-diagram-label");
        var box = await label.BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");
        var (left, top) = await OriginAsync();
        await DragAsync((float)(box.X + 10 - left), (float)(box.Y + (box.Height / 2) - top), 0, 20, true, null);

        await Expect(rect).ToHaveAttributeAsync("transform", "translate(530,220)");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task WaitModeCommitsTheMove(string mode)
    {
        opened = await app.OpenAsync("?wait=true", mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");

        await DragAsync(250, 170, 60, 40, true, null);

        await Expect(node).ToHaveAttributeAsync("transform", "translate(286,186)");
        await Expect(node).Not.ToHaveAttributeAsync("data-pending", string.Empty);
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

    private async Task DragAsync(float x, float y, float dx, float dy, bool alt, Func<Task>? beforeRelease)
    {
        var page = opened!.Page;
        var (left, top) = await OriginAsync().ConfigureAwait(true);
        var startX = (float)left + x;
        var startY = (float)top + y;
        if (alt)
        {
            await page.Keyboard.DownAsync("Alt").ConfigureAwait(true);
        }

        await page.Mouse.MoveAsync(startX, startY).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(startX + dx, startY + dy, new() { Steps = 10 }).ConfigureAwait(true);
        if (beforeRelease is not null)
        {
            await beforeRelease().ConfigureAwait(true);
        }

        await page.Mouse.UpAsync().ConfigureAwait(true);
        if (alt)
        {
            await page.Keyboard.UpAsync("Alt").ConfigureAwait(true);
        }
    }
}
