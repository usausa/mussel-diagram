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
        Assert.Equal(["server", "database", "balancer", "cloud", "vpc", "subnet", "process", "task", "decision"], names);
        await Expect(picker.Locator(".mussel-diagram-picker-item[data-type='decision'] polygon.mussel-diagram-picker-shape")).ToHaveCountAsync(1);

        await opened.Page.Mouse.ClickAsync(4, 4);
        await Expect(picker).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task RejectedPortsShowTheReasonWhileAndAfterDragging(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var links = opened.View.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(5);

        await DragAsync(420, 185, -90, 0, async () =>
        {
            await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='job'] circle[data-port='in'][data-not-allowed]")).ToHaveCountAsync(1).ConfigureAwait(true);
            await Expect(opened.View).ToHaveClassAsync(new Regex("mussel-diagram-not-allowed")).ConfigureAwait(true);
            await Expect(opened.View.Locator(".mussel-diagram-overlay .mussel-diagram-preview-link")).ToHaveAttributeAsync("marker-end", new Regex("^url\\(#")).ConfigureAwait(true);
        });

        await Expect(opened.View.Locator(".mussel-diagram-reason")).ToBeVisibleAsync();
        await Expect(opened.View).Not.ToHaveClassAsync(new Regex("mussel-diagram-not-allowed"));
        await Expect(links).ToHaveCountAsync(5);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task CommandKeysWaitUntilTheDragEnds(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var web1 = opened.View.Locator("g.mussel-diagram-node[data-id='web1']");

        await DragAsync(254, 174, 60, 40, async () => await opened.Page.Keyboard.PressAsync("Delete").ConfigureAwait(true));

        await Expect(web1).Not.ToHaveAttributeAsync("transform", "translate(230,150)");
        await Expect(web1).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Delete");
        await Expect(web1).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task KeysOutsideTypingWorkWhileEditingText(string mode)
    {
        opened = await app.OpenAsync("overlay", mode);
        var view = opened.View;
        await opened.Page.Locator("[data-action='validate']").ClickAsync();
        await Expect(view.Locator("g.mussel-diagram-node[data-id='archive']")).ToHaveClassAsync(new Regex("mussel-diagram-error"));
        var label = await view.Locator("g.mussel-diagram-node[data-id='notify'] > text.mussel-diagram-label").BoundingBoxAsync() ?? throw new InvalidOperationException("The label is not shown.");

        await opened.Page.Mouse.DblClickAsync((float)(label.X + (label.Width / 2)), (float)(label.Y + (label.Height / 2)));
        var editor = view.Locator("textarea.mussel-diagram-text-editor");
        await Expect(editor).ToBeVisibleAsync();
        await opened.Page.Keyboard.PressAsync("F8");

        await Expect(view.Locator("g.mussel-diagram-node[data-selected]:is([data-id='legacy'], [data-id='archive'])")).ToHaveCountAsync(1);
        await Expect(view.Locator("g.mussel-diagram-node[data-id='notify'][data-selected]")).ToHaveCountAsync(0);
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
