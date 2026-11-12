namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ViewE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private DiagramPage? opened;

    public static TheoryData<string, string> Modes => new()
    {
        { "server", "server" },
        { "wasm", "webassembly" },
        { "auto", "auto" }
    };

    public static TheoryData<string> ModeNames => ["server", "wasm", "auto"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ViewStartsInEveryRenderMode(string mode, string markerType)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        Assert.Contains($"\"type\":\"{markerType}\"", opened.InitialHtml, StringComparison.Ordinal);
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas, svg.mussel-diagram-overlay")).ToHaveCountAsync(2);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] text tspan").First).ToHaveTextAsync("星 1");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheContentShowsOnlyAfterTheFirstViewIsApplied(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var state = await opened.Page.EvaluateAsync<string[]>(
            """
            () => {
                const root = document.querySelector('.mussel-diagram');
                const stage = root.querySelector('.mussel-diagram-stage');
                const transform = root.querySelector('svg.mussel-diagram-canvas > g.mussel-diagram-viewport').getAttribute('transform');
                const shown = getComputedStyle(stage).visibility;
                root.removeAttribute('data-attached');
                const hidden = getComputedStyle(stage).visibility;
                root.setAttribute('data-attached', '');
                return [transform, shown, hidden];
            }
            """);

        Assert.NotEqual("translate(0,0) scale(1)", state[0]);
        Assert.Equal("visible", state[1]);
        Assert.Equal("hidden", state[2]);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task WheelPansAndControlWheelZooms(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        await Expect(viewport).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");

        await opened.Page.Mouse.MoveAsync(600, 400);
        await opened.Page.Mouse.WheelAsync(0, 120);
        await Expect(viewport).ToHaveAttributeAsync("transform", "translate(0,-120) scale(1)");

        await opened.Page.Keyboard.DownAsync("Control");
        await opened.Page.Mouse.WheelAsync(0, -100);
        await opened.Page.Keyboard.UpAsync("Control");
        await Expect(viewport).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\.16\)$"));
        await Expect(opened.View.Locator(".mussel-diagram-stage")).ToHaveAttributeAsync("style", string.Empty);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task DraggingANodeMovesItWithItsLinks(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='star1']");
        var line = opened.View.Locator("g.mussel-diagram-link[data-id='star1-triangle'] > path.mussel-diagram-link-line");
        await Expect(node).ToHaveAttributeAsync("transform", "translate(226,146)");
        var path = await line.GetAttributeAsync("d") ?? string.Empty;
        var version = await opened.View.GetAttributeAsync("data-version") ?? string.Empty;

        await opened.Page.Keyboard.DownAsync("Alt");
        await DragAsync(opened.Page, node.Locator("use"), 60, 40);
        await opened.Page.Keyboard.UpAsync("Alt");

        await Expect(opened.View).Not.ToHaveAttributeAsync("data-version", version);
        await Expect(node).ToHaveAttributeAsync("transform", "translate(286,186)");
        await Expect(line).Not.ToHaveAttributeAsync("d", path);
        await Expect(opened.Page.Locator(".mussel-diagram-overlay rect.mussel-diagram-selection")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task RejectedMoveGoesBackAndShowsTheReason(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var node = opened.View.Locator("g.mussel-diagram-node[data-id='pin']");
        var version = await opened.View.GetAttributeAsync("data-version") ?? string.Empty;

        await DragAsync(opened.Page, node.Locator("use"), 80, 0);

        await Expect(opened.View.Locator(".mussel-diagram-reason")).ToHaveTextAsync("この例ではピンを動かせません");
        await Expect(node).ToHaveAttributeAsync("transform", "translate(46,156)");
        await Expect(opened.View).ToHaveAttributeAsync("data-version", version);
    }

    private static async Task DragAsync(IPage page, ILocator handle, float dx, float dy)
    {
        var box = await handle.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The element is not shown.");
        var x = box.X + (box.Width / 2);
        var y = box.Y + (box.Height / 2);
        await page.Mouse.MoveAsync(x, y).ConfigureAwait(true);
        await page.Mouse.DownAsync().ConfigureAwait(true);
        await page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = 8 }).ConfigureAwait(true);
        await page.Mouse.UpAsync().ConfigureAwait(true);
    }
}
