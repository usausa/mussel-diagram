namespace MusselDiagram.E2ETests;

public sealed class DiagramPage(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public string InitialHtml { get; set; } = string.Empty;

    public ILocator View => page.Locator(".mussel-diagram");

    public Task WaitForViewAsync() =>
        page.Locator(".mussel-diagram[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
