namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ViewE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private SamplePage? opened;

    public static TheoryData<string, string> ModeMarkers => new()
    {
        { "server", "server" },
        { "wasm", "webassembly" }
    };

    public static TheoryData<string> Modes => ["server", "wasm"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(ModeMarkers))]
    public async Task ViewStartsInEveryRenderMode(string mode, string markerType)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        Assert.Contains($"\"type\":\"{markerType}\"", opened.InitialHtml, StringComparison.Ordinal);
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas, svg.mussel-diagram-overlay")).ToHaveCountAsync(2);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'] text tspan").First).ToHaveTextAsync("星 1");
    }

    [Theory]
    [MemberData(nameof(Modes))]
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
    [MemberData(nameof(Modes))]
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
    [MemberData(nameof(Modes))]
    public async Task ResettingTheZoomRightAfterAWheelZoomEndsAtOneHundredPercent(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        await Expect(viewport).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        await opened.View.FocusAsync();

        await opened.Page.Mouse.MoveAsync(600, 400);
        await opened.Page.Keyboard.DownAsync("Control");
        await opened.Page.Mouse.WheelAsync(0, -100);
        await opened.Page.Keyboard.PressAsync("0");
        await opened.Page.Keyboard.UpAsync("Control");

        await Expect(opened.View.Locator(".mussel-diagram-stage")).ToHaveAttributeAsync("style", string.Empty);
        await Expect(viewport).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\)$"));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task APressRightAfterAWheelZoomStartsFromTheNewZoom(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        await page.EvaluateAsync(
            """
            () => {
                const later = window.setTimeout;
                window.heldSettles = 0;
                window.setTimeout = (callback, delay, ...args) => (delay === 180 ? (window.heldSettles++, 0) : later(callback, delay, ...args));
                window.addEventListener('pointerdown', () => {
                    window.pressedStyle = document.querySelector('.mussel-diagram-stage').getAttribute('style') ?? '';
                });
            }
            """);

        await page.Mouse.MoveAsync(600, 400);
        await page.Keyboard.DownAsync("Control");
        await page.Mouse.WheelAsync(0, -100);
        await page.Keyboard.UpAsync("Control");
        await page.WaitForFunctionAsync("() => window.heldSettles > 0");
        await page.Mouse.DownAsync();
        await page.Mouse.UpAsync();

        Assert.Equal(string.Empty, await page.EvaluateAsync<string>("() => window.pressedStyle"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWheelZoomWhileAPressOrAPanGoesOnIsFoldedAtOnce(bool pan)
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var button = pan ? MouseButton.Middle : MouseButton.Left;
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        await page.EvaluateAsync(
            """
            () => {
                const later = window.setTimeout;
                window.heldSettles = 0;
                window.setTimeout = (callback, delay, ...args) => (delay === 180 ? (window.heldSettles++, 0) : later(callback, delay, ...args));
            }
            """);

        await page.Mouse.MoveAsync(600, 400);
        await page.Mouse.DownAsync(new() { Button = button });
        await page.Keyboard.DownAsync("Control");
        await page.Mouse.WheelAsync(0, -100);
        await page.Keyboard.UpAsync("Control");
        await page.WaitForFunctionAsync("() => window.heldSettles > 0");
        var stage = await page.EvaluateAsync<string>("() => document.querySelector('.mussel-diagram-stage').style.transform");
        await page.Mouse.UpAsync(new() { Button = button });

        Assert.Equal(string.Empty, stage);
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).Not.ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWheelZoomDuringADragFoldsAtOnceAndSettlesOnTheGrid(bool external)
    {
        opened = await app.OpenAsync(string.Empty, "server");
        var page = opened.Page;
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        await Expect(viewport).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        await page.Locator(".mussel-diagram-palette[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.EvaluateAsync(
            """
            () => {
                const later = window.setTimeout;
                window.heldSettles = [];
                window.setTimeout = (callback, delay, ...args) => (delay === 180 ? (window.heldSettles.push(() => callback(...args)), 0) : later(callback, delay, ...args));
            }
            """);
        var handle = external ? page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']") : opened.View.Locator("g.mussel-diagram-node[data-id='star1'] use");
        var from = await handle.BoundingBoxAsync() ?? throw new InvalidOperationException("The element is not shown.");
        var view = await opened.View.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");

        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(view.X + 400, view.Y + 300, new() { Steps = 8 });
        await page.Keyboard.DownAsync("Control");
        await page.Mouse.WheelAsync(0, -10);
        await page.Mouse.WheelAsync(0, -10);
        await page.Keyboard.UpAsync("Control");
        await page.WaitForFunctionAsync("() => window.heldSettles.length > 0");
        var folded = Scale(await viewport.GetAttributeAsync("transform"));
        await page.EvaluateAsync("() => window.heldSettles.splice(0).forEach((settle) => settle())");
        var settled = Scale(await viewport.GetAttributeAsync("transform"));
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();

        Assert.True(folded > 1, $"{folded}");
        Assert.True(Math.Abs((folded * 100) - Math.Round(folded * 100)) > 1e-6, $"{folded}");
        Assert.Equal(Math.Round(settled * 100), settled * 100, 6);
    }

    [Fact]
    public async Task AFrameOnlyMoveKeepsTheSelectionFramesAndMovesItsOwnFrame()
    {
        opened = await app.OpenAsync("large", "server");
        var page = opened.Page;
        await opened.View.FocusAsync();
        await page.Keyboard.PressAsync("Control+0");
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\)$"));
        Assert.True(await page.EvaluateAsync<bool>(
            """
            () => {
                const view = document.querySelector('.mussel-diagram').getBoundingClientRect();
                const node = [...document.querySelectorAll('g.mussel-diagram-node')].find((x) => {
                    const r = x.getBoundingClientRect();
                    return (r.width >= 40) && (r.height >= 30) && (r.left > view.left + 100) && (r.right < view.right - 100) && (r.top > view.top + 100) && (r.bottom < view.bottom - 100);
                });
                node?.setAttribute('data-picked', '');
                return !!node;
            }
            """));
        var node = opened.View.Locator("g.mussel-diagram-node[data-picked]");
        await node.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.mussel-diagram-overlay rect.mussel-diagram-selection').length > 32");
        var from = await node.BoundingBoxAsync() ?? throw new InvalidOperationException("The node is not shown.");
        var x = from.X + (from.Width / 2);
        var y = from.Y + (from.Height / 2);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 20, y + 10, new() { Steps = 4 });
        var frame = page.Locator(".mussel-diagram-drag-frame");
        await Expect(frame).ToHaveCountAsync(1);
        var left = await frame.GetAttributeAsync("x");
        await page.EvaluateAsync("() => { window.firstSelection = document.querySelector('.mussel-diagram-overlay rect.mussel-diagram-selection'); }");
        await page.Mouse.MoveAsync(x + 60, y + 30, new() { Steps = 4 });
        await Expect(frame).Not.ToHaveAttributeAsync("x", left ?? string.Empty);
        var kept = await page.EvaluateAsync<bool>("() => document.querySelector('.mussel-diagram-overlay rect.mussel-diagram-selection') === window.firstSelection");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();

        Assert.True(kept);
    }

    [Theory]
    [MemberData(nameof(Modes))]
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
    [MemberData(nameof(Modes))]
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

    private static double Scale(string? transform)
    {
        var text = transform!;
        return Double.Parse(text[(text.IndexOf("scale(", StringComparison.Ordinal) + "scale(".Length)..^1], CultureInfo.InvariantCulture);
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
