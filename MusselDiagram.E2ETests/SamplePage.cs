namespace MusselDiagram.E2ETests;

using System.Collections.Concurrent;

public sealed class SamplePage(IBrowserContext context, IPage page, Func<string> appLog) : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> events = new();

    public IPage Page => page;

    public ILocator View => page.Locator(".mussel-diagram");

    public void Watch()
    {
        page.Console += (_, m) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} console {m.Type}: {m.Text}");
        page.PageError += (_, e) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} pageerror: {e}");
        page.RequestFailed += (_, r) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} requestfailed: {r.Url} {r.Failure}");
        page.FrameNavigated += (_, f) => events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} navigated: {f.Url}");
    }

    public Task WaitForDiagramAsync() =>
        page.WaitForFunctionAsync(
            "() => { const roots = document.querySelectorAll('.mussel-diagram, .mussel-diagram-palette, .mussel-diagram-drag-source'); return (roots.length > 0) && [...roots].every((root) => root.hasAttribute('data-attached')); }",
            null,
            new() { Timeout = 30_000 });

    public Task RejectCallsAsync(params string[] names) =>
        page.EvaluateAsync(
            """
            names => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.rejected = [];
                window.unhandled = 0;
                window.addEventListener('unhandledrejection', () => { window.unhandled++; });
                proto.invokeMethodAsync = function (name, ...args) {
                    if (names.includes(name)) {
                        window.rejected.push(name);
                        return Promise.reject(new Error(name));
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """,
            names);

    public async Task<int> UnhandledRejectionsAsync(int calls)
    {
        await page.WaitForFunctionAsync("calls => window.rejected.length >= calls", calls).ConfigureAwait(true);
        return await page.EvaluateAsync<int>("() => new Promise(resolve => setTimeout(() => resolve(window.unhandled), 300))").ConfigureAwait(true);
    }

    public ValueTask DisposeAsync() => CloseAsync(TestContext.Current.TestState?.Result == TestResult.Failed);

    public async ValueTask CloseAsync(bool failed)
    {
        try
        {
            if (failed && (Environment.GetEnvironmentVariable("MUSSELDIAGRAM_E2E_DUMP") is { Length: > 0 } folder))
            {
                await DumpAsync(folder).ConfigureAwait(false);
            }
        }
        finally
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DumpAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        var name = String.Concat((TestContext.Current.Test?.TestDisplayName ?? "test").Select(static x => Char.IsAsciiLetterOrDigit(x) ? x : '_'));
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
                        reconnect: document.querySelector('#components-reconnect-modal')?.outerHTML?.slice(0, 200) ?? null
                    });
                }
                """).ConfigureAwait(false);
        }
        catch (PlaywrightException e)
        {
            state = e.Message;
        }

        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(folder, name + ".png") }).ConfigureAwait(false);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            state += $"{Environment.NewLine}screenshot: {e.Message}";
        }

        await File.WriteAllTextAsync(Path.Combine(folder, name + ".txt"), $"{state}{Environment.NewLine}{String.Join(Environment.NewLine, events)}{Environment.NewLine}--- app{Environment.NewLine}{appLog()}").ConfigureAwait(false);
    }
}
