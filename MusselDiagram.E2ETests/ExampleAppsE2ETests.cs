namespace MusselDiagram.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ExampleAppsE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
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
    public async Task TheGridSwitchesBetweenLinesAndNone(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var button = opened.Page.Locator("button[aria-label='グリッド']");
        var pattern = opened.View.Locator("g.mussel-diagram-grid pattern");
        await Expect(button).ToHaveAttributeAsync("data-grid", "lines");
        await Expect(pattern.Locator("path").First).ToBeAttachedAsync();
        await Expect(pattern.Locator("circle")).ToHaveCountAsync(0);

        await button.ClickAsync();
        await Expect(button).ToHaveAttributeAsync("data-grid", "none");
        await Expect(pattern).ToHaveCountAsync(0);

        await button.ClickAsync();
        await Expect(button).ToHaveAttributeAsync("data-grid", "lines");
        await Expect(pattern.Locator("path").First).ToBeAttachedAsync();
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheWorkflowChecksPortsAndReachAndEditsSettings(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var view = opened.View;
        var agent = view.Locator("g.mussel-diagram-node[data-id='agent']");
        await Expect(view.Locator("g.mussel-diagram-node.mussel-diagram-error, g.mussel-diagram-node.mussel-diagram-warning")).ToHaveCountAsync(0);

        await ClickAsync("g.mussel-diagram-node[data-id='model'] > .mussel-diagram-shape");
        await opened.Page.Keyboard.PressAsync("Delete");
        await Expect(agent).ToHaveClassAsync(new Regex("mussel-diagram-error"));
        await opened.Page.Keyboard.PressAsync("Control+Z");
        await Expect(agent).Not.ToHaveClassAsync(new Regex("mussel-diagram-error"));

        await ClickAsync("g.mussel-diagram-node[data-id='fetch'] > .mussel-diagram-shape");
        var name = opened.Page.Locator("[data-panel='settings'] input").First;
        await Expect(name).ToHaveValueAsync("受注を取る");
        await name.FillAsync("注文を取る");
        await name.PressAsync("Tab");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='fetch'] > text.mussel-diagram-label[data-label='注文を取る']")).ToHaveCountAsync(1);

        await ClickAsync("g.mussel-diagram-node[data-id='fetch'] > .mussel-diagram-shape");
        await opened.Page.Keyboard.PressAsync("Delete");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='check']")).ToHaveClassAsync(new Regex("mussel-diagram-warning"));
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheErDiagramRevealsKeysAndEditsColumns(string mode)
    {
        opened = await app.OpenAsync("er", mode);
        var view = opened.View;
        var page = opened.Page;
        var links = view.Locator("g.mussel-diagram-link");
        var revealed = view.Locator("circle.mussel-diagram-port[data-revealed]");
        await Expect(links).ToHaveCountAsync(3);
        await Expect(view.Locator("circle.mussel-diagram-port[data-direction='in']").First).ToBeHiddenAsync();

        var (x, y) = await CenterAsync("g.mussel-diagram-node[data-id='products'] circle.mussel-diagram-port[data-port='c3:out']");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 60, y + 40, new() { Steps = 6 });
        await Expect(revealed).ToHaveCountAsync(3);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await Expect(revealed).ToHaveCountAsync(0);

        await ClickAsync("g.mussel-diagram-node[data-id='orders'] .er-header");
        await page.Locator("[data-action='add-column']").ClickAsync();
        var rows = view.Locator("g.mussel-diagram-node[data-id='orders'] g.er-row");
        await Expect(rows).ToHaveCountAsync(4);
        await page.Locator("[data-panel='table'] [data-column='c2'] button[aria-label='列を消す']").ClickAsync();
        await Expect(rows).ToHaveCountAsync(3);
        await Expect(links).ToHaveCountAsync(2);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheMonitorShowsLiveStatesAndDetails(string mode)
    {
        opened = await app.OpenAsync("monitor", mode);
        var view = opened.View;
        var page = opened.Page;
        var badges = view.Locator(".mussel-diagram-badge-set[data-for='router']");
        await Expect(badges).ToContainTextAsync("Mbps");
        var before = await badges.TextContentAsync();
        await Expect(badges).Not.ToHaveTextAsync(before ?? string.Empty, new() { Timeout = 8000 });

        await page.Locator(".monitor-item[data-device='server-a']").ClickAsync();
        await Expect(page.Locator(".monitor-detail[data-device='server-a']")).ToBeVisibleAsync();
        await Expect(view.Locator("g.mussel-diagram-node[data-id='server-a'][data-selected]")).ToHaveCountAsync(1);

        var router = view.Locator("g.mussel-diagram-node[data-id='router']");
        var transform = await router.GetAttributeAsync("transform");
        var (x, y) = await CenterAsync("g.mussel-diagram-node[data-id='router'] > .mussel-diagram-shape");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 80, y + 40, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        await Expect(router).ToHaveAttributeAsync("transform", transform ?? string.Empty);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarSwitchesWhatABackgroundDragDoes(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var page = opened.Page;
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        var box = opened.View.Locator(".mussel-diagram-overlay rect.mussel-diagram-box-selection");
        var view = await opened.View.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");
        var (x, y) = (view.X + 600, view.Y + 740);
        await Expect(page.Locator("[data-tool='box-select']")).ToHaveAttributeAsync("aria-pressed", "true");

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 100, y - 100, new() { Steps = 8 });
        await Expect(box).ToHaveCountAsync(1);
        await page.Mouse.UpAsync();

        await page.Locator("[data-tool='pan']").ClickAsync();
        await Expect(page.Locator("[data-tool='pan']")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas")).ToHaveClassAsync(new Regex("mussel-diagram-pan-drag"));
        var (beforeX, beforeY) = Translation(await viewport.GetAttributeAsync("transform"));
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 100, y - 100, new() { Steps = 8 });
        await Expect(box).ToHaveCountAsync(0);
        await page.Mouse.UpAsync();
        var (afterX, afterY) = Translation(await viewport.GetAttributeAsync("transform"));
        Assert.Equal(beforeX - 100, afterX, 1);
        Assert.Equal(beforeY - 100, afterY, 1);

        await page.Locator("[data-tool='none']").ClickAsync();
        await Expect(page.Locator("[data-tool='none']")).ToHaveAttributeAsync("aria-pressed", "true");
        var still = await viewport.GetAttributeAsync("transform") ?? string.Empty;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 100, y - 100, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        await Expect(box).ToHaveCountAsync(0);
        await Expect(viewport).ToHaveAttributeAsync("transform", still);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarZoomsWithButtonsAndTheMenu(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var page = opened.Page;
        var viewport = opened.View.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport");
        var label = page.Locator("[data-action='zoom-menu'] button").First;
        await Expect(label).ToHaveTextAsync(new Regex("^[0-9]+%$"));
        var fitted = (await label.TextContentAsync())?.Trim() ?? string.Empty;
        var start = await viewport.GetAttributeAsync("transform") ?? string.Empty;

        await page.Locator("[data-action='zoom-in']").ClickAsync();
        await Expect(label).Not.ToHaveTextAsync(fitted);
        await Expect(viewport).Not.ToHaveAttributeAsync("transform", start);

        await label.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "200%" }).ClickAsync();
        await Expect(viewport).ToHaveAttributeAsync("transform", new Regex(@"scale\(2\)$"));
        await Expect(label).ToHaveTextAsync("200%");

        await page.Locator("[data-action='zoom-out']").ClickAsync();
        await Expect(label).Not.ToHaveTextAsync("200%");

        await page.Locator("[data-action='zoom-fit']").ClickAsync();
        await Expect(label).ToHaveTextAsync(fitted);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheSingleLineDiagramChecksCapacityAndShowsDeadParts(string mode)
    {
        opened = await app.OpenAsync("electric", mode);
        var view = opened.View;
        var page = opened.Page;
        var transformer = view.Locator("g.mussel-diagram-node[data-id='tr2']");
        await Expect(transformer).ToHaveClassAsync(new Regex("mussel-diagram-warning"));
        await Expect(transformer.Locator(":scope > title")).ToHaveTextAsync(new Regex("負荷の合計 60 kVA が容量 50 kVA を超えています"));
        await Expect(view.Locator(".mussel-diagram-badge-set[data-for='tr2'] .mussel-diagram-badge[data-tone='error'] text")).ToHaveTextAsync("点検期限切れ");
        await Expect(view.Locator(".mussel-diagram-badge-set[data-for='vcb'] .mussel-diagram-badge[data-tone='warning'] text")).ToHaveTextAsync(new Regex("^点検 [0-9]+/[0-9]+$"));
        await Expect(page.Locator(".ledger-item[data-device='tr2']")).ToHaveAttributeAsync("data-status", "expired");

        await ClickAsync("g.mussel-diagram-node[data-id='m2'] > .mussel-diagram-shape");
        var capacity = page.Locator("[data-panel='ledger'] input[data-field='capacity']");
        await Expect(capacity).ToHaveValueAsync("30");
        await capacity.FillAsync("10");
        await capacity.PressAsync("Tab");
        await Expect(transformer).Not.ToHaveClassAsync(new Regex("mussel-diagram-warning"));

        await ClickAsync("g.mussel-diagram-node[data-id='mccb-m1'] > .mussel-diagram-shape");
        await page.Locator("[data-panel='ledger'] label:has(input[data-field='closed'])").ClickAsync();
        await Expect(view.Locator(".mussel-diagram-badge-set[data-for='mccb-m1'] .mussel-diagram-badge[data-tone='warning'] text")).ToHaveTextAsync("切");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='m1']")).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-override-opacity: 0.35;"));
        await Expect(view.Locator("g.mussel-diagram-link[data-id='mccb-m1-m1']")).ToHaveAttributeAsync("style", new Regex("override-opacity"));
        await Expect(view.Locator("g.mussel-diagram-node[data-id='m2']")).Not.ToHaveAttributeAsync("style", new Regex("override-opacity"));
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheSingleLineDiagramConnectsOnlyTheSameVoltage(string mode)
    {
        opened = await app.OpenAsync("electric", mode);
        var view = opened.View;
        var page = opened.Page;
        var high = view.Locator("circle.mussel-diagram-port[data-kind='hv'][data-revealed]");
        var low = view.Locator("circle.mussel-diagram-port[data-kind='lv'][data-revealed]");
        await Expect(view.Locator("circle.mussel-diagram-port[data-direction='in']").First).ToBeHiddenAsync();

        var (x, y) = await CenterAsync("g.mussel-diagram-node[data-id='mccb-s'] circle.mussel-diagram-port[data-port='out']");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 40, y + 60, new() { Steps = 6 });
        await Expect(low.First).ToBeAttachedAsync();
        await Expect(high).ToHaveCountAsync(0);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await Expect(low).ToHaveCountAsync(0);

        await page.Locator(".ledger-item[data-device='tr2']").ClickAsync();
        await Expect(view.Locator("g.mussel-diagram-node[data-id='tr2'][data-selected]")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-panel='ledger'] input[data-field='name']")).ToHaveValueAsync("TR2 動力");
    }

    private static (double X, double Y) Translation(string? transform)
    {
        var match = Regex.Match(transform ?? string.Empty, @"translate\((?<x>[-0-9.e]+),(?<y>[-0-9.e]+)\)");
        return (double.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture), double.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture));
    }

    private async Task<(float X, float Y)> CenterAsync(string selector)
    {
        var box = await opened!.View.Locator(selector).First.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The element is not shown.");
        return (box.X + (box.Width / 2), box.Y + (box.Height / 2));
    }

    private async Task ClickAsync(string selector)
    {
        var (x, y) = await CenterAsync(selector).ConfigureAwait(true);
        await opened!.Page.Mouse.ClickAsync(x, y).ConfigureAwait(true);
    }
}
