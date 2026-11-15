namespace MusselDiagram.E2ETests;

using System.Text;
using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class ExampleAppsE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private DiagramPage? opened;

    public static TheoryData<string> ModeNames => ["server", "wasm"];

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
        await Expect(pattern).ToHaveAttributeAsync("x", "-5");
        await Expect(pattern).ToHaveAttributeAsync("width", "50");
        await Expect(pattern.Locator("path.mussel-diagram-grid-major")).ToHaveAttributeAsync("d", "M5,0V50M0,5H50");
        await Expect(pattern.Locator("path.mussel-diagram-grid-major")).ToHaveAttributeAsync("stroke-width", "2");
        await Expect(pattern.Locator("path.mussel-diagram-grid-major")).ToHaveCSSAsync("shape-rendering", "crispedges");
        await Expect(pattern.Locator("path.mussel-diagram-grid-minor")).ToHaveCSSAsync("shape-rendering", "crispedges");

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
    public async Task TheToolbarSwitchesWhatADragDoes(string mode)
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
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas")).ToHaveClassAsync(new Regex("mussel-diagram-pan-tool"));
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
        await Expect(opened.View.Locator("svg.mussel-diagram-canvas")).Not.ToHaveClassAsync(new Regex("mussel-diagram-pan-tool"));
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
    public async Task TheToolbarZoomsToTheSelection(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var label = page.Locator("[data-action='zoom-menu'] button").First;
        await Expect(label).ToHaveTextAsync(new Regex("^[0-9]+%$"));
        var fitted = (await label.TextContentAsync())?.Trim() ?? string.Empty;

        await ClickAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='rect']")).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Locator("[data-action='zoom-selection']").ClickAsync();
        await Expect(label).Not.ToHaveTextAsync(fitted);

        var view = await opened.View.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");
        var (x, y) = await CenterAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");
        Assert.InRange(x, view.X + (view.Width / 2) - 2, view.X + (view.Width / 2) + 2);
        Assert.InRange(y, view.Y + (view.Height / 2) - 2, view.Y + (view.Height / 2) + 2);
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

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarDeletesUndoesAndPastes(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var nodes = opened.View.Locator("g.mussel-diagram-node");
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var star = opened.View.Locator("g.mussel-diagram-node[data-id='star2']");
        await Expect(nodes).ToHaveCountAsync(9);
        await ClickAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);

        await page.Locator("[data-action='delete']").ClickAsync();
        await Expect(rect).ToHaveCountAsync(0);
        await page.Locator("[data-action='undo']").ClickAsync();
        await Expect(rect).ToHaveCountAsync(1);
        await page.Locator("[data-action='redo']").ClickAsync();
        await Expect(rect).ToHaveCountAsync(0);
        await Expect(nodes).ToHaveCountAsync(8);

        await ClickAsync("g.mussel-diagram-node[data-id='star1'] > .mussel-diagram-shape");
        await page.Locator("[data-action='copy']").ClickAsync();
        await page.Locator("[data-action='paste']").ClickAsync();
        await Expect(nodes).ToHaveCountAsync(9);

        await ClickAsync("g.mussel-diagram-node[data-id='star2'] > .mussel-diagram-shape");
        await Expect(star).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Locator("[data-action='cut']").ClickAsync();
        await Expect(star).ToHaveCountAsync(0);
        await Expect(nodes).ToHaveCountAsync(8);
        await page.Locator("[data-action='paste']").ClickAsync();
        await Expect(nodes).ToHaveCountAsync(9);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarChangesTheOrderOfTheSelection(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var nodes = opened.View.Locator(".mussel-diagram-nodes > g.mussel-diagram-node");
        var count = await nodes.CountAsync();
        await ClickAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");

        await opened.Page.Locator("[data-action='send-to-back']").ClickAsync();
        await Expect(nodes.Nth(0)).ToHaveAttributeAsync("data-id", "rect");
        await opened.Page.Locator("[data-action='bring-forward']").ClickAsync();
        await Expect(nodes.Nth(1)).ToHaveAttributeAsync("data-id", "rect");
        await opened.Page.Locator("[data-action='bring-to-front']").ClickAsync();
        await Expect(nodes.Nth(count - 1)).ToHaveAttributeAsync("data-id", "rect");
        await opened.Page.Locator("[data-action='send-backward']").ClickAsync();
        await Expect(nodes.Nth(count - 2)).ToHaveAttributeAsync("data-id", "rect");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarGroupsLocksAndAligns(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var page = opened.Page;
        var groups = opened.View.Locator("g.mussel-diagram-group");
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var hexagon = opened.View.Locator("g.mussel-diagram-node[data-id='hexagon']");
        await Expect(groups).ToHaveCountAsync(4);
        await ClickAsync("g.mussel-diagram-node[data-id='ellipse'] > .mussel-diagram-shape");
        await ShiftClickAsync("g.mussel-diagram-node[data-id='hexagon'] > .mussel-diagram-shape");
        await Expect(hexagon).ToHaveAttributeAsync("data-selected", string.Empty);

        await page.Locator("[data-action='group']").ClickAsync();
        await Expect(groups).ToHaveCountAsync(5);
        await Expect(opened.View.Locator("g.mussel-diagram-group[data-selected]")).ToHaveCountAsync(1);
        await page.Locator("[data-action='ungroup']").ClickAsync();
        await Expect(groups).ToHaveCountAsync(4);

        await ClickAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");
        await Expect(rect).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Locator("[data-action='lock']").ClickAsync();
        await Expect(rect).ToHaveClassAsync(new Regex("mussel-diagram-locked"));

        await ClickAsync("g.mussel-diagram-node[data-id='ellipse'] > .mussel-diagram-shape");
        await ShiftClickAsync("g.mussel-diagram-node[data-id='hexagon'] > .mussel-diagram-shape");
        await Expect(hexagon).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Locator("[data-action='align-menu'] button").First.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "上に揃える", Exact = true }).ClickAsync();
        await Expect(hexagon).ToHaveAttributeAsync("transform", "translate(320,600)");

        await ShiftClickAsync("g.mussel-diagram-node[data-id='card'] > .mussel-diagram-shape");
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='card']")).ToHaveAttributeAsync("data-selected", string.Empty);
        await page.Locator("[data-action='align-menu'] button").First.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "左右に等間隔", Exact = true }).ClickAsync();
        await Expect(hexagon).ToHaveAttributeAsync("transform", "translate(310,600)");
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarDownloadsSvgAndPng(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);

        var svg = await DownloadAsync("SVG", "diagram.svg");
        Assert.Contains("<svg", Encoding.UTF8.GetString(svg), StringComparison.Ordinal);

        var png = await DownloadAsync("PNG", "diagram.png");
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheEditButtonsAreOffOnAReadOnlyPage(string mode)
    {
        opened = await app.OpenAsync("monitor", mode);
        var page = opened.Page;

        foreach (var action in new[] { "undo", "redo", "cut", "paste", "delete", "bring-to-front", "bring-forward", "send-backward", "send-to-back", "group", "ungroup", "lock" })
        {
            await Expect(page.Locator($"[data-action='{action}']")).ToBeDisabledAsync();
        }

        await Expect(page.Locator("[data-action='align-menu'] button").First).ToBeDisabledAsync();
        await Expect(page.Locator("[data-action='copy']")).ToBeEnabledAsync();
        await Expect(page.Locator("[data-action='export-menu'] button").First).ToBeEnabledAsync();
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheToolbarWrapsInANarrowWindow(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        await opened.Page.SetViewportSizeAsync(1100, 900);
        var toolbar = opened.Page.Locator(".view-toolbar");
        var items = toolbar.Locator(":scope > *");
        var bar = await toolbar.BoundingBoxAsync() ?? throw new InvalidOperationException("The toolbar is not shown.");
        var count = await items.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var box = await items.Nth(i).BoundingBoxAsync() ?? throw new InvalidOperationException($"The item {i} is not shown.");
            Assert.True((box.X + box.Width) <= (bar.X + bar.Width), $"The item {i} sticks out.");
        }

        string[][] groups =
        [
            ["undo", "redo"],
            ["cut", "copy", "paste", "delete"],
            ["box-select", "pan", "none"],
            ["zoom-out", "zoom-menu", "zoom-in", "zoom-fit", "zoom-selection"],
            ["bring-to-front", "bring-forward", "send-backward", "send-to-back"],
            ["group", "ungroup", "lock", "align-menu"]
        ];
        foreach (var group in groups)
        {
            var first = await MiddleAsync(group[0]);
            foreach (var name in group[1..])
            {
                Assert.True(Math.Abs(await MiddleAsync(name) - first) <= 4, $"'{name}' is not on the row of '{group[0]}'.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheBasicPageShowsEveryElementBesideThePanel(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var view = await opened.View.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");

        foreach (var id in new[] { "frame", "group", "left", "right", "pin", "star1", "star2", "triangle", "rect", "diamond", "ellipse", "hexagon", "card" })
        {
            var element = opened.View.Locator($"g[data-id='{id}']:is(.mussel-diagram-node, .mussel-diagram-group)");
            await Expect(element).ToBeAttachedAsync();
            var box = await element.BoundingBoxAsync() ?? throw new InvalidOperationException($"'{id}' is not shown.");
            Assert.True((box.X >= view.X) && (box.Y >= view.Y) && ((box.X + box.Width) <= (view.X + view.Width)) && ((box.Y + box.Height) <= (view.Y + view.Height)), id);
        }
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheBasicPageChangesTheLookOfASelectedLink(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var link = opened.View.Locator("g.mussel-diagram-link[data-id='rect-diamond']");
        var line = link.Locator("path.mussel-diagram-link-line");
        await ClickAsync("g.mussel-diagram-link[data-id='rect-diamond'] > path.mussel-diagram-link-hit");

        await ChooseAsync("link-dash", "点線");
        await Expect(link).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-link-dash: 2 3;"));
        await ChooseAsync("link-width", "3");
        await Expect(link).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-link-stroke-width: 3;"));
        await ChooseAsync("link-stroke", "赤");
        await Expect(link).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-link-stroke: #E53935;"));
        await ChooseAsync("link-target-marker", "ひし形");
        var diamond = opened.View.Locator("defs > marker[style='--mussel-diagram-marker-color: #E53935']:has(path[d='M0,5L5,0L10,5L5,10Z'])");
        await Expect(diamond).ToHaveCountAsync(1);
        await Expect(line).ToHaveAttributeAsync("marker-end", $"url(#{await diamond.GetAttributeAsync("id")})");
        await ChooseAsync("link-source-marker", "円");
        await Expect(line).ToHaveAttributeAsync("marker-start", new Regex("^url\\(#"));
        await ChooseAsync("link-form", "曲線");
        await Expect(line).ToHaveAttributeAsync("d", new Regex("C"));

        await ClickBackgroundAsync();
        await opened.Page.Keyboard.PressAsync("Control+Z");
        await Expect(line).Not.ToHaveAttributeAsync("d", new Regex("C"));
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task TheBasicPageChangesTheLookOfSelectedShapesTogether(string mode)
    {
        opened = await app.OpenAsync(string.Empty, mode);
        var rect = opened.View.Locator("g.mussel-diagram-node[data-id='rect']");
        var diamond = opened.View.Locator("g.mussel-diagram-node[data-id='diamond']");
        await ClickAsync("g.mussel-diagram-node[data-id='rect'] > .mussel-diagram-shape");
        var (x, y) = await CenterAsync("g.mussel-diagram-node[data-id='diamond'] > .mussel-diagram-shape");
        await opened.Page.Keyboard.DownAsync("Shift");
        await opened.Page.Mouse.ClickAsync(x, y);
        await opened.Page.Keyboard.UpAsync("Shift");

        await ChooseAsync("box-fill", "青");
        await ChooseAsync("box-text", "白");
        await ChooseAsync("box-width", "3");
        foreach (var node in new[] { rect, diamond })
        {
            await Expect(node).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-shape-fill: #1E88E5;.*--mussel-diagram-shape-stroke-width: 3;.*--mussel-diagram-text-color: #FFFFFF;"));
        }

        await ChooseAsync("box-fill", "型のまま");
        await Expect(rect).Not.ToHaveAttributeAsync("style", new Regex("#1E88E5"));
        await Expect(diamond).Not.ToHaveAttributeAsync("style", new Regex("#1E88E5"));

        await ClickBackgroundAsync();
        await opened.Page.Keyboard.PressAsync("Control+Z");
        await Expect(rect).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-shape-fill: #1E88E5;"));
        await Expect(diamond).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-shape-fill: #1E88E5;"));
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

    private async Task<float> MiddleAsync(string name)
    {
        var box = await opened!.Page.Locator($".view-toolbar [data-action='{name}'], .view-toolbar [data-tool='{name}']").BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException($"'{name}' is not shown.");
        return box.Y + (box.Height / 2);
    }

    private async Task ShiftClickAsync(string selector)
    {
        var (x, y) = await CenterAsync(selector).ConfigureAwait(true);
        await opened!.Page.Keyboard.DownAsync("Shift").ConfigureAwait(true);
        await opened.Page.Mouse.ClickAsync(x, y).ConfigureAwait(true);
        await opened.Page.Keyboard.UpAsync("Shift").ConfigureAwait(true);
    }

    private async Task<byte[]> DownloadAsync(string format, string name)
    {
        var page = opened!.Page;
        await page.Locator("[data-action='export-menu'] button").First.ClickAsync().ConfigureAwait(true);
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Menuitem, new() { Name = format, Exact = true }).ClickAsync()).ConfigureAwait(true);
        Assert.Equal(name, download.SuggestedFilename);
        var path = await download.PathAsync().ConfigureAwait(true);
        return await File.ReadAllBytesAsync(path).ConfigureAwait(true);
    }

    private async Task ClickBackgroundAsync()
    {
        var box = await opened!.View.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The view is not shown.");
        await opened.Page.Mouse.ClickAsync(box.X + 40, box.Y + box.Height - 40).ConfigureAwait(true);
    }

    private async Task ChooseAsync(string field, string option)
    {
        await opened!.Page.Locator($"[data-panel='look'] [data-field='{field}']").ClickAsync().ConfigureAwait(true);
        await opened.Page.Locator(".mud-popover-open").GetByRole(AriaRole.Option, new() { Name = option, Exact = true }).ClickAsync().ConfigureAwait(true);
    }
}
