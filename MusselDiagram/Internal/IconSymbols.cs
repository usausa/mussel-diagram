namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record IconSymbol(string Id, double Width, double Height, string Body);

internal sealed class IconSymbols : IDisposable
{
    private readonly Func<Action, Task> invoke;

    private readonly CancellationTokenSource cancellation = new();

    private readonly List<string> wanted = [];

    private readonly HashSet<string> wantedReferences = new(StringComparer.Ordinal);

    private readonly Dictionary<DiagramIcon, IconContent?> contents = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<DiagramIcon, Task> loading = new(ReferenceEqualityComparer.Instance);

    private RenderContext? context;

    private ImmutableArray<IconSymbol>? symbols;

    private bool disposed;

    public IconSymbols(Func<Action, Task> invoke)
    {
        this.invoke = invoke;
    }

    public event Action? Changed;

    public int Version { get; private set; }

    public ImmutableArray<IconSymbol> Symbols => symbols ??= Build();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void Use(RenderContext renderContext)
    {
        context = renderContext;
        foreach (var reference in wanted)
        {
            Start(reference);
        }

        Invalidate();
    }

    public void Want(string reference)
    {
        if (!wantedReferences.Add(reference))
        {
            return;
        }

        wanted.Add(reference);
        if (Start(reference))
        {
            Invalidate();
        }
    }

    public Task WhenLoadedAsync() => Task.WhenAll(loading.Values);

    private bool Start(string reference)
    {
        if (context?.FindIcon(reference) is not { } entry)
        {
            return false;
        }

        if (contents.TryGetValue(entry.Icon, out var content))
        {
            return content is not null;
        }

        if (entry.Icon.Load is null)
        {
            contents[entry.Icon] = SvgSanitizer.Content(entry.Set, entry.Icon);
            return true;
        }

        if (!disposed && !loading.ContainsKey(entry.Icon))
        {
            var task = LoadAsync(entry);
            if (!task.IsCompleted)
            {
                loading[entry.Icon] = task;
            }
        }

        return false;
    }

    private async Task LoadAsync(IconEntry entry)
    {
        var content = await SvgSanitizer.LoadAsync(entry.Set, entry.Icon, cancellation.Token).ConfigureAwait(false);
        await invoke(() =>
        {
            loading.Remove(entry.Icon);
            contents[entry.Icon] = content;
            if (content is not null)
            {
                Invalidate();
            }
        }).ConfigureAwait(false);
    }

    private void Invalidate()
    {
        symbols = null;
        Version++;
        Changed?.Invoke();
    }

    private ImmutableArray<IconSymbol> Build()
    {
        var builder = ImmutableArray.CreateBuilder<IconSymbol>();
        foreach (var reference in wanted)
        {
            if ((context?.FindIcon(reference) is { } entry) && contents.TryGetValue(entry.Icon, out var content) && (content is not null))
            {
                builder.Add(new IconSymbol(entry.Id, content.Width, content.Height, content.Body));
            }
        }

        return builder.ToImmutable();
    }
}
