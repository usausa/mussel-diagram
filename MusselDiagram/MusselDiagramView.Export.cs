namespace MusselDiagram;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private const long MaxExportSize = 1L << 30;

    private static readonly TimeSpan RouteWait = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan RoutePoll = TimeSpan.FromMilliseconds(50);

    private readonly List<TaskCompletionSource> exportRenders = [];

    private int exporting;

    private bool exportRendered;

    public Task<DiagramExportResult> ExportSvgAsync(DiagramSvgExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DiagramSvgExportOptions();
        return ExportAsync(
            new ScriptExportOptions
            {
                Format = "svg",
                Margin = options.Margin,
                Background = options.Background == DiagramExportBackground.Theme,
                Overlays = options.IncludeOverlays,
                Fonts = options.EmbedFonts
            },
            cancellationToken);
    }

    public Task<DiagramExportResult> ExportImageAsync(DiagramImageExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DiagramImageExportOptions();
        var jpeg = options.Format == DiagramImageFormat.Jpeg;
        return ExportAsync(
            new ScriptExportOptions
            {
                Format = jpeg ? "jpeg" : "png",
                Margin = options.Margin,
                Background = jpeg || (options.Background == DiagramExportBackground.Theme),
                Overlays = options.IncludeOverlays,
                Fonts = true,
                Scale = options.Scale,
                Quality = options.Quality
            },
            cancellationToken);
    }

    private async Task<DiagramExportResult> ExportAsync(ScriptExportOptions options, CancellationToken cancellationToken)
    {
        var current = controller ?? throw new InvalidOperationException("The view is not shown yet.");
        WantIcons(null);
        await Symbols.WhenLoadedAsync().WaitAsync(cancellationToken).ConfigureAwait(true);
        var started = Stopwatch.GetTimestamp();
        while (!IsDisposed && HasProvisionalRoutes() && (Stopwatch.GetElapsedTime(started) < RouteWait))
        {
            await Task.Delay(RoutePoll, cancellationToken).ConfigureAwait(true);
        }

        if (IsDisposed)
        {
            return new DiagramExportResult(null, null, []);
        }

        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        exportRenders.Add(rendered);
        exporting++;
        exportRendered = false;
        StateHasChanged();
        try
        {
            await rendered.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            if (IsDisposed)
            {
                return new DiagramExportResult(null, null, []);
            }

            var json = await current.InvokeAsync<string>("exportDiagram", cancellationToken, ScriptJson.ToElement(options)).ConfigureAwait(true);
            if (IsDisposed)
            {
                return new DiagramExportResult(null, null, []);
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var issues = MissingImages(root.GetProperty("missing"));
            if (root.GetProperty("empty").GetBoolean())
            {
                return new DiagramExportResult(null, null, issues);
            }

            if (root.GetProperty("tooLarge").GetBoolean())
            {
                return new DiagramExportResult(null, Options.Localize(new DiagramReason(DiagramReasonKind.TooLarge)), issues);
            }

            var reference = await current.InvokeAsync<IJSStreamReference>("takeExport", cancellationToken, root.GetProperty("id").GetInt32()).ConfigureAwait(true);
            await using (reference.ConfigureAwait(true))
            {
                if (IsDisposed)
                {
                    return new DiagramExportResult(null, null, []);
                }

                var source = await reference.OpenReadStreamAsync(MaxExportSize, cancellationToken).ConfigureAwait(true);
                await using (source.ConfigureAwait(true))
                {
                    var content = new MemoryStream();
                    await source.CopyToAsync(content, cancellationToken).ConfigureAwait(true);
                    content.Position = 0;
                    return new DiagramExportResult(content, null, issues);
                }
            }
        }
        catch (JSDisconnectedException)
        {
            return new DiagramExportResult(null, null, []);
        }
        catch (Exception ex) when (IsDisposed && (ex is ObjectDisposedException or JSException))
        {
            return new DiagramExportResult(null, null, []);
        }
        finally
        {
            exportRenders.Remove(rendered);
            exporting--;
            if (!IsDisposed)
            {
                StateHasChanged();
            }
        }
    }

    private bool HasProvisionalRoutes()
    {
        foreach (var link in Snapshot.Links)
        {
            if (link.RouteState == DiagramRouteState.Provisional)
            {
                return true;
            }
        }

        return false;
    }

    private void CompleteExportRender()
    {
        if (exportRendered)
        {
            exportRendered = false;
            EndExportRenders();
        }
    }

    private void EndExportRenders()
    {
        foreach (var rendered in exportRenders)
        {
            rendered.TrySetResult();
        }
    }

    private ImmutableArray<DiagramIssue> MissingImages(JsonElement missing)
    {
        var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
        foreach (var item in missing.EnumerateArray())
        {
            var url = item.GetProperty("url").GetString() ?? string.Empty;
            string[] ids = [.. item.GetProperty("ids").EnumerateArray().Select(static x => x.GetString()).OfType<string>()];
            issues.Add(Options.Localize(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.ImageNotEmbedded, [.. ids], [url])));
        }

        return issues.ToImmutable();
    }
}
