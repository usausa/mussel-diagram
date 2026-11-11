namespace MusselDiagram.E2ETests;

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
        await Expect(opened.View.Locator("svg")).ToHaveCountAsync(2);
    }
}
