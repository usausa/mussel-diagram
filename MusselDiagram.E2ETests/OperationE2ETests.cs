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
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='web1']");

        await DragAsync(254, 174, 60, 40, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay line.mussel-diagram-guide").First).ToBeAttachedAsync().ConfigureAwait(true));

        await Expect(node).ToHaveAttributeAsync("transform", "translate(282,186)");
        await Expect(opened.View.Locator(".mussel-diagram-overlay line.mussel-diagram-guide")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ResizingANodeChangesItsBounds(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var shape = opened.View.Locator("g.mussel-diagram-node[data-id='job'] > rect.mussel-diagram-shape");
        await ClickAsync(375, 185);
        await Expect(opened.View.Locator(".mussel-diagram-overlay [data-handle]")).ToHaveCountAsync(8);

        await DragAsync(420, 210, 30, 60, false, null);

        await Expect(shape).ToHaveAttributeAsync("width", "120");
        await Expect(shape).ToHaveAttributeAsync("height", "110");
        await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-resize-frame")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ConnectingFromAPortAddsALink(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(5);

        await DragAsync(420, 185, 184, 49, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveCountAsync(1).ConfigureAwait(true));

        await Expect(links).ToHaveCountAsync(6);
        await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ReleasingALinkOnEmptySpaceOpensTheTypePicker(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var picker = opened.View.Locator(".mussel-diagram-picker");
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        await Expect(nodes).ToHaveCountAsync(6);

        await DragAsync(420, 185, 480, 115, false, null);
        await Expect(picker).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("Escape");
        await Expect(picker).ToHaveCountAsync(0);

        await DragAsync(420, 185, 480, 115, false, null);
        await opened.Page.Keyboard.TypeAsync("data");
        await Expect(picker.Locator(".mussel-diagram-picker-item")).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Enter");

        await Expect(nodes).ToHaveCountAsync(7);
        await Expect(opened.View.Locator("g.mussel-diagram-link")).ToHaveCountAsync(6);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-type='database']")).ToHaveCountAsync(2);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ReconnectingALinkEndMovesIt(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var hit = opened.View.Locator("g.mussel-diagram-link[data-id='lb-web1'] > path.mussel-diagram-link-hit");
        await ClickAsync(200, 174);
        var handle = opened.View.Locator(".mussel-diagram-overlay [data-end='target'][data-id='lb-web1']");
        await Expect(handle).ToHaveCountAsync(1);

        await DragAsync(230, 174, 374, 60, false, null);

        await Expect(hit).ToHaveAttributeAsync("d", new System.Text.RegularExpressions.Regex("L580,234$"));
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task BoxSelectionSelectsTheElementsInside(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        await DragAsync(215, 140, 85, 205, false, async () =>
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-box-selection")).ToHaveCountAsync(1).ConfigureAwait(true));

        await Expect(opened.View.Locator(".mussel-diagram-overlay rect.mussel-diagram-selection")).ToHaveCountAsync(2);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='web2']")).ToHaveAttributeAsync("data-selected", string.Empty);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task MovingANodeIntoAGroupChangesItsParent(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var job = opened.View.Locator("g.mussel-diagram-node[data-id='job']");

        await DragAsync(375, 185, 200, 40, true, async () =>
            await Expect(opened.View.Locator("g.mussel-diagram-group[data-id='private']")).ToHaveAttributeAsync("data-drop-target", string.Empty).ConfigureAwait(true));
        await Expect(job).ToHaveAttributeAsync("transform", "translate(530,200)");

        var label = opened.View.Locator("g.mussel-diagram-group[data-id='private'] > text.mussel-diagram-label");
        var box = await label.BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");
        var (left, top) = await OriginAsync();
        await DragAsync((float)(box.X + 10 - left), (float)(box.Y + (box.Height / 2) - top), 0, 20, true, null);

        await Expect(job).ToHaveAttributeAsync("transform", "translate(530,220)");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task WaitModeCommitsTheMove(string mode)
    {
        opened = await app.OpenAsync("?wait=true", mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='web1']");

        await DragAsync(254, 174, 60, 40, true, null);

        await Expect(node).ToHaveAttributeAsync("transform", "translate(290,190)");
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
