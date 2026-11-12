namespace MusselDiagram.E2ETests;

using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using static Microsoft.Playwright.Assertions;

public sealed class OverlayE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
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
    public async Task IssuesAreShownAndVisitedWithF8(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var view = opened.View;
        await DeleteAsync("model");
        await DeleteAsync("fetch");

        var agent = view.Locator("g.mussel-diagram-node[data-id='agent']");
        await Expect(agent).ToHaveClassAsync(new Regex("mussel-diagram-error"));
        await Expect(view.Locator("g.mussel-diagram-node[data-id='reply']")).ToHaveClassAsync(new Regex("mussel-diagram-warning"));
        await Expect(agent.Locator(":scope > title")).ToHaveTextAsync("返信を考える\nポート「model」に線をつないでください\n開始から届きません");
        var badge = view.Locator(".mussel-diagram-badge-set[data-for='agent'] > .mussel-diagram-badge[data-tone='error']");
        await Expect(badge).ToHaveAttributeAsync("transform", "translate(52,0)");

        await view.FocusAsync();
        var first = view.Locator("g.mussel-diagram-node[data-id='agent'][data-selected]");
        for (var attempt = 0; ; attempt++)
        {
            await opened.Page.Keyboard.PressAsync("F8");
            var shown = true;
            try
            {
                await Expect(first).ToHaveCountAsync(1, new() { Timeout = 2000 });
            }
            catch (PlaywrightException) when (attempt < 2)
            {
                shown = false;
            }

            if (shown)
            {
                break;
            }

            await Expect(view.Locator("[data-selected]")).ToHaveCountAsync(0);
        }

        await opened.Page.Keyboard.PressAsync("F8");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='reply'][data-selected]")).ToHaveCountAsync(1);
        await opened.Page.Keyboard.PressAsync("Shift+F8");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='agent'][data-selected]")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task BadgesShowStatesReportClicksAndFollowDrags(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        var view = opened.View;
        var run = opened.Page.Locator("[data-action='run']");

        await run.ClickAsync();
        await Expect(view.Locator("g.mussel-diagram-node.mussel-diagram-state-running")).ToHaveCountAsync(1);
        await Expect(run).ToBeDisabledAsync();
        await Expect(view.Locator("g.mussel-diagram-node[data-id='reply']")).ToHaveClassAsync(new Regex("mussel-diagram-state-done"), new() { Timeout = 15000 });
        await Expect(run).ToBeEnabledAsync();

        var fetch = view.Locator("g.mussel-diagram-node[data-id='fetch']");
        var badges = view.Locator(".mussel-diagram-badge-set[data-for='fetch']");
        await Expect(fetch).ToHaveClassAsync(new Regex("mussel-diagram-state-done"));
        await Expect(badges.Locator(".mussel-diagram-badge[data-corner='top-left'] text")).ToHaveTextAsync("完了");
        await Expect(view.Locator("g.mussel-diagram-node[data-id='model']")).ToHaveClassAsync(new Regex("mussel-diagram-state-done"));
        await Expect(view.Locator("g.mussel-diagram-node[data-id='idle']")).ToHaveAttributeAsync("style", new Regex("--mussel-diagram-override-opacity: 0.45;"));
        await Expect(view.Locator("g.mussel-diagram-link[data-id='fetch-check']")).ToHaveClassAsync(new Regex("mussel-diagram-state-flowed"));
        await Expect(view.Locator("g.mussel-diagram-link[data-id='check-idle']")).Not.ToHaveClassAsync(new Regex("mussel-diagram-state-flowed"));
        var line = view.Locator(".mussel-diagram-badge-set[data-for='fetch-check'][data-link]");
        await Expect(line).ToHaveAttributeAsync("transform", "translate(310,150)");
        await Expect(line.Locator("text")).ToHaveTextAsync("12 件");

        var runs = badges.Locator(".mussel-diagram-badge[data-clickable]");
        await Expect(runs).ToHaveAttributeAsync("transform", "translate(52,60)");
        await runs.ClickAsync();
        await Expect(runs.Locator("text")).ToHaveTextAsync("2");
        await Expect(view.Locator("[data-selected]")).ToHaveCountAsync(0);

        var box = await fetch.Locator(":scope > .mussel-diagram-shape").BoundingBoxAsync() ?? throw new InvalidOperationException("The node is not shown.");
        await opened.Page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await opened.Page.Mouse.DownAsync();
        await opened.Page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2) + 120, new() { Steps = 8 });
        await Expect(badges).Not.ToHaveAttributeAsync("transform", "translate(200,120)");
        await opened.Page.Mouse.UpAsync();

        var moved = await fetch.GetAttributeAsync("transform");
        Assert.NotEqual("translate(200,120)", moved);
        await Expect(badges).ToHaveAttributeAsync("transform", moved!);
    }

    [Theory]
    [MemberData(nameof(ModeNames))]
    public async Task ExportsSvgPngAndJpeg(string mode)
    {
        opened = await app.OpenAsync("workflow", mode);
        await opened.Page.Locator("[data-action='run']").ClickAsync();
        await Expect(opened.View.Locator("g.mussel-diagram-node[data-id='reply']")).ToHaveClassAsync(new Regex("mussel-diagram-state-done"), new() { Timeout = 15000 });
        await DeleteAsync("model");
        await Expect(opened.View.Locator(".mussel-diagram-badge-set[data-for='agent'] > .mussel-diagram-badge[data-tone='error']")).ToHaveCountAsync(1);

        var svg = XDocument.Parse(Encoding.UTF8.GetString(await DownloadAsync("svg")));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var root = svg.Root!;
        var width = int.Parse(root.Attribute("width")!.Value, CultureInfo.InvariantCulture);
        var height = int.Parse(root.Attribute("height")!.Value, CultureInfo.InvariantCulture);
        Assert.Equal($"0 0 {width} {height}", root.Attribute("viewBox")!.Value);
        Assert.Empty(root.Descendants(ns + "use"));
        Assert.Empty(root.Descendants(ns + "title"));
        Assert.DoesNotContain(root.Descendants(), static x => x.Attribute("class") is not null);
        Assert.Contains(root.Descendants(ns + "tspan"), static x => x.Value == "毎朝の受注チェック");
        Assert.Contains(root.Descendants(ns + "text"), static x => x.Value == "12 件");
        Assert.Contains(root.Descendants(ns + "text"), static x => x.Value == "完了");
        var agent = root.Descendants(ns + "g").Single(static x => x.Attribute("data-id")?.Value == "agent");
        Assert.NotNull(agent.Elements(ns + "rect").First().Attribute("stroke"));
        Assert.Contains(root.Descendants(ns + "g"), static x => x.Attribute("data-id")?.Value == "idle");

        var png = await DownloadAsync("png");
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal(width * 2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(height * 2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

        var jpeg = await DownloadAsync("jpeg");
        Assert.Equal([0xFF, 0xD8, 0xFF], jpeg[..3]);
    }

    private async Task DeleteAsync(string id)
    {
        var node = opened!.View.Locator($"g.mussel-diagram-node[data-id='{id}']");
        var box = await node.Locator(":scope > .mussel-diagram-shape").BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The node is not shown.");
        await opened.Page.Mouse.ClickAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2)).ConfigureAwait(true);
        await Expect(node).ToHaveAttributeAsync("data-selected", string.Empty).ConfigureAwait(true);
        await opened.Page.Keyboard.PressAsync("Delete").ConfigureAwait(true);
        await Expect(node).ToHaveCountAsync(0).ConfigureAwait(true);
    }

    private async Task<byte[]> DownloadAsync(string action)
    {
        var page = opened!.Page;
        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator($"[data-action='{action}']").ClickAsync()).ConfigureAwait(true);
        var path = await download.PathAsync().ConfigureAwait(true);
        return await File.ReadAllBytesAsync(path).ConfigureAwait(true);
    }
}
