namespace MusselDiagram.E2ETests;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class AwsE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private SamplePage? opened;

    public static TheoryData<string> Modes => ["server", "wasm"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ImportingThePackageRegistersTheIcons(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var view = opened.View;
        var web = view.Locator("g.mussel-diagram-node[data-id='web-a']");
        await Expect(web).ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));
        await Expect(view.Locator("g.mussel-diagram-group[data-id='vpc'] use.mussel-diagram-icon")).ToHaveCountAsync(0);

        await opened.Page.Locator("[data-action='import']").SetInputFilesAsync(new FilePayload { Name = "Icon-package.zip", MimeType = "application/zip", Buffer = CreatePackage() });

        await Expect(web.Locator("use.mussel-diagram-icon")).ToHaveCountAsync(1);
        await Expect(web).Not.ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));
        await Expect(view.Locator("g.mussel-diagram-group[data-id='vpc'] use.mussel-diagram-icon")).ToHaveCountAsync(1);
        await Expect(view.Locator("g.mussel-diagram-group[data-id='cloud'] use.mussel-diagram-icon")).ToHaveCountAsync(1);
        await Expect(Symbols(view)).ToHaveCountAsync(5);
        await opened.Page.Locator(".mussel-diagram-palette input").FillAsync("Amazon EC2");
        await Expect(opened.Page.Locator(".mussel-diagram-palette-item")).ToHaveCountAsync(2);
        await Expect(opened.Page.Locator(".mussel-diagram-palette-item[data-mussel-type='aws:Res_Amazon-EC2_Instance']")).ToHaveTextAsync("Amazon EC2 Instance");
        await Expect(Symbols(view)).ToHaveCountAsync(6);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task AnUnreadablePackageShowsAMessage(string mode)
    {
        opened = await app.OpenAsync("aws", mode);

        await opened.Page.Locator("[data-action='import']").SetInputFilesAsync(new FilePayload { Name = "broken.zip", MimeType = "application/zip", Buffer = "not a zip"u8.ToArray() });

        var message = opened.Page.GetByText("zip ファイルとして読めませんでした。");
        await Expect(message).ToBeVisibleAsync();
        await opened.Page.Locator(".diagram-sidebar .mud-alert-close button").ClickAsync();
        await Expect(message).ToHaveCountAsync(0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task APackageWithoutIconsKeepsTheTypesAndIssuesAreCounted(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var page = opened.Page;
        var input = page.Locator("[data-action='import']");
        var web = opened.View.Locator("g.mussel-diagram-node[data-id='web-a']");
        await input.SetInputFilesAsync(new FilePayload { Name = "Icon-package.zip", MimeType = "application/zip", Buffer = CreatePackage() });
        await Expect(web.Locator("use.mussel-diagram-icon")).ToHaveCountAsync(1);

        await input.SetInputFilesAsync(new FilePayload { Name = "other.zip", MimeType = "application/zip", Buffer = CreateZip([("readme.txt", "No icons")]) });

        await Expect(page.GetByText("この zip には使えるアイコンがありません。")).ToBeVisibleAsync();
        await Expect(web.Locator("use.mussel-diagram-icon")).ToHaveCountAsync(1);
        await Expect(web).Not.ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));

        await input.SetInputFilesAsync(new FilePayload { Name = "Icon-package.zip", MimeType = "application/zip", Buffer = CreateZip([.. PackageFiles(), ("Icon-package_07312026/Architecture-Service-Icons_07312026/Arch_Compute/64/Arch_Amazon-Broken_64.svg", "<svg")]) });

        await Expect(page.GetByText("アイコンの取り込みで 1 件の問題がありました。")).ToBeVisibleAsync();
        await Expect(web.Locator("use.mussel-diagram-icon")).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CancelingTheFilePickerKeepsTheMessage(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var page = opened.Page;
        var input = page.Locator("[data-action='import']");
        await input.SetInputFilesAsync(new FilePayload { Name = "broken.zip", MimeType = "application/zip", Buffer = "not a zip"u8.ToArray() });
        var message = page.GetByText("zip ファイルとして読めませんでした。");
        await Expect(message).ToBeVisibleAsync();
        var zoom = page.Locator("[data-action='zoom-menu'] button").First;
        var before = await zoom.InnerTextAsync();

        await input.SetInputFilesAsync(Array.Empty<FilePayload>());
        await page.Locator("[data-action='zoom-in']").ClickAsync();
        await Expect(zoom).Not.ToHaveTextAsync(before);

        await Expect(message).ToBeVisibleAsync();
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ATooLargePackageShowsTheLimit(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        try
        {
            using (var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.Write))
            {
                RandomAccess.SetLength(handle, (256L * 1024 * 1024) + 1);
            }

            await opened.Page.Locator("[data-action='import']").SetInputFilesAsync(path);

            var message = opened.Page.GetByText("256 MB を超える zip は読み込めません。");
            await Expect(message).ToBeVisibleAsync();
            await opened.Page.Locator(".diagram-sidebar .mud-alert-close button").ClickAsync();
            await Expect(message).ToHaveCountAsync(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ThePackageInTheIconFolderIsLoadedAtStart(string mode)
    {
        app.ProvideAwsIcons(PackageFiles());
        try
        {
            opened = await app.OpenAsync("aws", mode);
            var web = opened.View.Locator("g.mussel-diagram-node[data-id='web-a']");

            await Expect(web.Locator("use.mussel-diagram-icon")).ToHaveCountAsync(1);
            await Expect(web).Not.ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));
            await Expect(opened.View.Locator("g.mussel-diagram-group[data-id='vpc'] use.mussel-diagram-icon")).ToHaveCountAsync(1);
            var id = (await web.Locator("use.mussel-diagram-icon").GetAttributeAsync("href"))![1..];
            await Expect(opened.View.Locator($".mussel-diagram-canvas > defs > symbol[id='{id}'] rect[fill='#ED7100']")).ToHaveCountAsync(1);
        }
        finally
        {
            app.RemoveAwsIcons();
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task OnlyTheShownIconsAreRead(string mode)
    {
        app.ProvideAwsIcons(PackageFiles());
        try
        {
            opened = await app.OpenAsync("aws", mode);
            var view = opened.View;
            await Expect(view.Locator("g.mussel-diagram-node[data-id='web-a']")).Not.ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));
            await Expect(Symbols(view)).ToHaveCountAsync(5);

            await opened.Page.Locator(".mussel-diagram-palette input").FillAsync("Amazon EC2");
            await Expect(opened.Page.Locator(".mussel-diagram-palette-item")).ToHaveCountAsync(2);
            await Expect(Symbols(view)).ToHaveCountAsync(6);

            await view.FocusAsync();
            await opened.Page.Keyboard.PressAsync("Control+Enter");
            var picker = view.Locator(".mussel-diagram-picker");
            await Expect(picker).ToBeVisibleAsync();
            await picker.Locator("input").FillAsync("Amazon S3");
            var item = picker.Locator(".mussel-diagram-picker-item");
            await Expect(item).ToHaveCountAsync(1);
            await Expect(Symbols(view)).ToHaveCountAsync(7);
            var id = await item.GetAttributeAsync("data-icon");
            await Expect(view.Locator($".mussel-diagram-canvas > defs > symbol[id='{id}'] rect[fill='#7AA116']")).ToHaveCountAsync(1);
        }
        finally
        {
            app.RemoveAwsIcons();
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task OverlayGroupsAreFramedAndTakeLinks(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var view = opened.View;
        var page = opened.Page;
        await page.Locator("[data-action='import']").SetInputFilesAsync(new FilePayload { Name = "Icon-package.zip", MimeType = "application/zip", Buffer = CreatePackage() });
        await Expect(view.Locator("g.mussel-diagram-node[data-id='alb']")).Not.ToHaveClassAsync(new Regex("mussel-diagram-unregistered"));
        await Expect(view.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).Not.ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        var (labelX, labelY) = await CenterAsync(view.Locator("g.mussel-diagram-group[data-id='scaling'] > text.mussel-diagram-label").First);

        await page.Mouse.ClickAsync(labelX, labelY);
        await Expect(view.Locator(".mussel-diagram-overlay rect.mussel-diagram-selection-overlay")).ToHaveCountAsync(1);

        var links = view.Locator("g.mussel-diagram-link");
        await Expect(links).ToHaveCountAsync(5);
        var (albX, albY) = await CenterAsync(view.Locator("g.mussel-diagram-node[data-id='alb']"));
        await page.Mouse.MoveAsync(albX - 2, albY - 2);
        await page.Mouse.MoveAsync(albX, albY);
        var arrow = view.Locator(".mussel-diagram-overlay [data-arrow='bottom']");
        await Expect(arrow).ToHaveCountAsync(1);
        var (startX, startY) = await CenterAsync(arrow);
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(labelX, labelY, new() { Steps = 10 });
        await Expect(view.Locator("g.mussel-diagram-group[data-id='scaling'][data-candidate]")).ToHaveCountAsync(1);
        await page.Mouse.UpAsync();

        await Expect(links).ToHaveCountAsync(6);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task OverlayGroupsFollowWhileTheyAreDragged(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var view = opened.View;
        var page = opened.Page;
        await Expect(view.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).Not.ToHaveAttributeAsync("transform", "translate(0,0) scale(1)");
        var group = view.Locator("g.mussel-diagram-group[data-id='scaling']");
        var before = await group.GetAttributeAsync("transform") ?? string.Empty;
        var (labelX, labelY) = await CenterAsync(group.Locator("> text.mussel-diagram-label").First);

        await page.Mouse.MoveAsync(labelX, labelY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(labelX + 40, labelY + 30, new() { Steps = 8 });

        await Expect(group).Not.ToHaveAttributeAsync("transform", before);
        await page.Mouse.UpAsync();
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SubnetsCannotBeDroppedAtTheTopLevel(string mode)
    {
        opened = await app.OpenAsync("aws", mode);
        var view = opened.View;
        var page = opened.Page;
        var zoom = page.Locator("[data-action='zoom-menu'] button").First;
        await Expect(zoom).ToHaveTextAsync(new Regex("^[0-9]+%$"));
        await zoom.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "50%", Exact = true }).ClickAsync();
        await Expect(view.Locator("svg.mussel-diagram-canvas > g.mussel-diagram-viewport")).ToHaveAttributeAsync("transform", new Regex(@"scale\(0\.5\)$"));
        var subnet = view.Locator("g.mussel-diagram-group[data-id='public-a']");
        var start = await subnet.GetAttributeAsync("transform") ?? string.Empty;
        var area = await view.BoundingBoxAsync() ?? throw new InvalidOperationException("The view is not shown.");
        var cloud = await view.Locator("g.mussel-diagram-group[data-id='cloud']").BoundingBoxAsync() ?? throw new InvalidOperationException("The cloud is not shown.");
        var (fromX, fromY) = await CenterAsync(view.Locator("g.mussel-diagram-group[data-id='public-a'] > text.mussel-diagram-label").First);
        var toX = (cloud.X + cloud.Width + area.X + area.Width) / 2;

        await page.Mouse.MoveAsync(fromX, fromY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(toX, fromY, new() { Steps = 10 });
        await page.Mouse.UpAsync();

        await Expect(subnet).ToHaveAttributeAsync("transform", start);
        await Expect(view.Locator(".mussel-diagram-reason")).ToHaveTextAsync(new Regex("ここには置けません|The element cannot be placed here"));
    }

    private static async Task<(float X, float Y)> CenterAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("The element is not shown.");
        return (box.X + (box.Width / 2), box.Y + (box.Height / 2));
    }

    private static ILocator Symbols(ILocator view) => view.Locator(".mussel-diagram-canvas > defs > symbol");

    private static byte[] CreatePackage() => CreateZip(PackageFiles());

    private static byte[] CreateZip(IEnumerable<(string Path, string Text)> files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), Encoding.UTF8);
                writer.Write(text);
            }
        }

        return stream.ToArray();
    }

    private static IEnumerable<(string Path, string Text)> PackageFiles()
    {
        const string root = "Icon-package_07312026/";
        yield return Icon(root + "Architecture-Service-Icons_07312026/Arch_Compute/64/Arch_Amazon-EC2_64.svg", 80, "#ED7100");
        yield return Icon(root + "Architecture-Service-Icons_07312026/Arch_Compute/48/Arch_Amazon-EC2_48.svg", 64, "#ED7100");
        yield return Icon(root + "Architecture-Service-Icons_07312026/Arch_Databases/64/Arch_Amazon-RDS_64.svg", 80, "#C925D1");
        yield return Icon(root + "Architecture-Service-Icons_07312026/Arch_Networking-Content-Delivery/64/Arch_Elastic-Load-Balancing_64.svg", 80, "#8C4FFF");
        yield return Icon(root + "Architecture-Service-Icons_07312026/Arch_Storage/64/Arch_Amazon-S3_64.svg", 80, "#7AA116");
        yield return Icon(root + "Resource-Icons_07312026/Res_Compute/Res_Amazon-EC2_Instance_48.svg", 48, "#ED7100");
        yield return Icon(root + "Category-Icons_07312026/Arch-Category_64/Arch-Category_Compute_64.svg", 90, "#ED7100");
        yield return Icon(root + "Architecture-Group-Icons_07312026/Virtual-private-cloud-VPC_32.svg", 40, "#8C4FFF");
        yield return Icon(root + "Architecture-Group-Icons_07312026/AWS-Cloud-logo_32.svg", 40, "#242F3E");
        yield return Icon(root + "Architecture-Group-Icons_07312026/AWS-Cloud-logo_32_Dark.svg", 40, "#FFFFFF");
        yield return Icon("__MACOSX/Icon-package_07312026/._Arch_Amazon-EC2_64.svg", 80, "#000000");
    }

    private static (string Path, string Text) Icon(string path, int size, string color) =>
        (path, string.Create(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {size} {size}\"><rect width=\"{size}\" height=\"{size}\" fill=\"{color}\" /></svg>"));
}
