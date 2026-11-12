namespace MusselDiagram.E2ETests;

using System.Collections.Concurrent;

public sealed class DiagramPage(IBrowserContext context, IPage page, Func<string> appLog) : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> events = new();

    public IPage Page => page;

    public string InitialHtml { get; set; } = string.Empty;

    public ILocator View => page.Locator(".mussel-diagram");

    public void Watch()
    {
        page.Console += (_, m) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} console {m.Type}: {m.Text}");
        page.PageError += (_, e) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} pageerror: {e}");
        page.RequestFailed += (_, r) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} requestfailed: {r.Url} {r.Failure}");
        page.FrameNavigated += (_, f) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} navigated: {f.Url}");
    }

    public Task WaitForViewAsync() =>
        page.Locator(".mussel-diagram[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

    public async ValueTask DisposeAsync()
    {
        if ((TestContext.Current.TestState?.Result == TestResult.Failed) && (Environment.GetEnvironmentVariable("MUSSELDIAGRAM_E2E_DUMP") is { Length: > 0 } folder))
        {
            await DumpAsync(folder).ConfigureAwait(false);
        }

        await context.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DumpAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        var name = string.Concat((TestContext.Current.Test?.TestDisplayName ?? "test").Select(static x => char.IsAsciiLetterOrDigit(x) ? x : '_'));
        string state;
        try
        {
            state = await page.EvaluateAsync<string>(
                """
                () => {
                    const view = document.querySelector('.mussel-diagram');
                    const error = document.getElementById('blazor-error-ui');
                    const run = document.querySelector("[data-action='run']");
                    return JSON.stringify({
                        url: location.href,
                        attached: view?.hasAttribute('data-attached'),
                        nodes: view?.querySelectorAll('g.mussel-diagram-node').length,
                        groups: view?.querySelectorAll('g.mussel-diagram-group').length,
                        links: view?.querySelectorAll('g.mussel-diagram-link').length,
                        width: view?.clientWidth,
                        height: view?.clientHeight,
                        viewport: view?.querySelector('.mussel-diagram-canvas > .mussel-diagram-viewport')?.getAttribute('transform'),
                        version: view?.getAttribute('data-version'),
                        errorUi: error ? getComputedStyle(error).display : null,
                        running: document.querySelectorAll('.mussel-diagram-state-running').length,
                        runDisabled: run ? run.disabled : null,
                        reconnect: document.querySelector('#components-reconnect-modal, .components-reconnect-show')?.outerHTML?.slice(0, 200) ?? null
                    });
                }
                """).ConfigureAwait(false);
            await page.ScreenshotAsync(new() { Path = Path.Combine(folder, name + ".png") }).ConfigureAwait(false);
        }
        catch (PlaywrightException e)
        {
            state = e.Message;
        }

        await File.WriteAllTextAsync(Path.Combine(folder, name + ".txt"), $"{state}{Environment.NewLine}{string.Join(Environment.NewLine, events)}{Environment.NewLine}--- app{Environment.NewLine}{appLog()}").ConfigureAwait(false);
    }
}
