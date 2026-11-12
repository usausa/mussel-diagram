namespace MusselDiagram.E2ETests;

using System.Text.Json;
using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class TouchE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private DiagramPage? opened;

    private ICDPSession? session;

    private (double X, double Y) origin;

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
    public async Task OneFingerPansAndTapsSelect(string mode)
    {
        await OpenAsync(mode);
        var viewport = opened!.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");

        await TouchAsync("touchStart", (700, 470));
        await MoveAsync([(700, 470)], [(700, 370)]);
        await TouchAsync("touchEnd");
        await Expect(viewport).ToHaveAttributeAsync("transform", new Regex(@"^translate\(0,-9\d\) scale\(1\)$"));

        await TouchAsync("touchStart", (250, 170 - 90));
        await TouchAsync("touchEnd");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task LongPressesToggleAndThenSelectAnArea(string mode)
    {
        await OpenAsync(mode);
        var selected = opened!.View.Locator("g.mussel-diagram-node[data-selected]");

        await TouchAsync("touchStart", (250, 170));
        await TouchAsync("touchEnd");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]")).ToHaveCountAsync(1);
        await LongPressAsync((250, 300));
        await Expect(selected).ToHaveCountAsync(2);
        await LongPressAsync((250, 170));
        await Expect(selected).ToHaveCountAsync(1);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star2'][data-selected]")).ToHaveCountAsync(1);

        await TouchAsync("touchStart", (455, 140));
        await Task.Delay(400, TestContext.Current.CancellationToken);
        await MoveAsync([(455, 140)], [(200, 360)]);
        await TouchAsync("touchEnd");
        await Expect(selected).ToHaveCountAsync(3);
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='star1'][data-selected]")).ToHaveCountAsync(1);
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ASecondFingerCancelsTheMoveAndPinchZooms(string mode)
    {
        await OpenAsync(mode);
        var star1 = opened!.View.Locator("g.mussel-diagram-node[data-id='star1']");
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");

        await TouchAsync("touchStart", (250, 170));
        await MoveAsync([(250, 170)], [(250, 230)]);
        await Expect(star1).Not.ToHaveAttributeAsync("transform", "translate(226,146)");

        await TouchAsync("touchStart", (250, 230), (450, 230));
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
        await MoveAsync([(250, 230), (450, 230)], [(200, 230), (500, 230)]);
        await TouchAsync("touchEnd");

        await Expect(opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(1\.[45]\d*\)$"));
        await Expect(star1).ToHaveAttributeAsync("transform", "translate(226,146)");
        await Expect(opened.View.Locator(".mussel-diagram-stage")).ToHaveAttributeAsync("style", string.Empty);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task PaletteItemsAreDraggedAfterALongPress(string mode)
    {
        await OpenAsync(mode);
        var nodes = opened!.View.Locator("g.mussel-diagram-node");
        await Expect(nodes).ToHaveCountAsync(9);
        await opened.Page.Locator(".mussel-diagram-palette[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var item = await opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='star']").BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var from = (X: item.X + (item.Width / 2) - origin.X, Y: item.Y + (item.Height / 2) - origin.Y);

        await TouchAsync("touchStart", from);
        await MoveAsync([from], [(from.X + 60, from.Y)]);
        await TouchAsync("touchEnd");
        await Expect(nodes).ToHaveCountAsync(9);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        await TouchAsync("touchStart", from);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        await MoveAsync([from], [(120, 520)]);
        await TouchAsync("touchEnd");
        await Expect(nodes).ToHaveCountAsync(10);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TappingAnElementShowsTheConnectArrows(string mode)
    {
        await OpenAsync(mode);
        var arrows = opened!.View.Locator(".mussel-diagram-overlay .mussel-diagram-arrow");

        await TouchAsync("touchStart", (550, 340));
        await TouchAsync("touchEnd");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='diamond'][data-selected]")).ToHaveCountAsync(1);
        await Expect(arrows).ToHaveCountAsync(4);

        await TouchAsync("touchStart", (700, 470));
        await TouchAsync("touchEnd");
        await Expect(arrows).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task DoubleTapsEditText(string mode)
    {
        await OpenAsync(mode);

        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        await Task.WhenAll(
            TouchAsync("touchStart", time, (250, 204)),
            TouchAsync("touchEnd", time + 0.06),
            TouchAsync("touchStart", time + 0.16, (250, 204)),
            TouchAsync("touchEnd", time + 0.22));

        await Expect(opened!.View.Locator("textarea.mussel-diagram-text-editor")).ToHaveValueAsync("星 1");
    }

    private async Task OpenAsync(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode, true).ConfigureAwait(true);
        session = await opened.Page.Context.NewCDPSessionAsync(opened.Page).ConfigureAwait(true);
        var box = await opened.View.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The view is not shown.");
        origin = (box.X, box.Y);
    }

    private async Task LongPressAsync((double X, double Y) point)
    {
        await TouchAsync("touchStart", point).ConfigureAwait(true);
        await Task.Delay(400, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await TouchAsync("touchEnd").ConfigureAwait(true);
    }

    private async Task MoveAsync((double X, double Y)[] from, (double X, double Y)[] to)
    {
        for (var step = 1; step <= 10; step++)
        {
            var points = from.Select((x, i) => (x.X + ((to[i].X - x.X) * step / 10), x.Y + ((to[i].Y - x.Y) * step / 10))).ToArray();
            await TouchAsync("touchMove", points).ConfigureAwait(true);
        }
    }

    private Task<JsonElement?> TouchAsync(string type, params (double X, double Y)[] points) =>
        session!.SendAsync("Input.dispatchTouchEvent", TouchEvent(type, points));

    private Task<JsonElement?> TouchAsync(string type, double time, params (double X, double Y)[] points)
    {
        var touch = TouchEvent(type, points);
        touch["timestamp"] = time;
        return session!.SendAsync("Input.dispatchTouchEvent", touch);
    }

    private Dictionary<string, object> TouchEvent(string type, IEnumerable<(double X, double Y)> points) => new()
    {
        ["type"] = type,
        ["touchPoints"] = points.Select((p, i) => new Dictionary<string, object> { ["x"] = origin.X + p.X, ["y"] = origin.Y + p.Y, ["id"] = i + 1 }).ToArray()
    };
}
