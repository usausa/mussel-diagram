namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class OverlayPage : IDisposable
{
    private static readonly string[] Steps = ["fetch", "transform", "load"];

    private static readonly string[] Outputs = ["fetch-transform", "transform-load", "load-notify"];

    private readonly Diagram diagram = SampleDiagrams.CreateFlow();

    private readonly Dictionary<string, int> runs = new(StringComparer.Ordinal);

    private MusselDiagramView? view;

    private IReadOnlyList<DiagramIssue>? issues;

    private Dictionary<string, DiagramOverlay> overlays = [];

    private int step;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private static IReadOnlyList<DiagramBadgeKind> BadgeKinds => SampleDiagrams.BadgeKinds;

    protected override void OnInitialized() => diagram.Changed += OnDiagramChanged;

    public void Dispose() => diagram.Changed -= OnDiagramChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            await view.ZoomToFitAsync();
        }
    }

    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        if (issues is not null)
        {
            issues = diagram.Validate();
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleIssues() => issues = issues is null ? diagram.Validate() : null;

    private async Task ExportSvgAsync()
    {
        if (view is not null)
        {
            await DownloadAsync(await view.ExportSvgAsync(new DiagramSvgExportOptions { IncludeOverlays = true, EmbedFonts = true }), "diagram.svg", "image/svg+xml");
        }
    }

    private async Task ExportImageAsync(DiagramImageFormat format)
    {
        if (view is not null)
        {
            var png = format == DiagramImageFormat.Png;
            var result = await view.ExportImageAsync(new DiagramImageExportOptions { Format = format, Scale = 2, IncludeOverlays = true });
            await DownloadAsync(result, png ? "diagram.png" : "diagram.jpg", png ? "image/png" : "image/jpeg");
        }
    }

    private async Task DownloadAsync(DiagramExportResult result, string name, string type)
    {
        if (result.Content is not { } content)
        {
            return;
        }

        await using (content)
        {
            using var stream = new DotNetStreamReference(content, true);
            await using var module = await Runtime.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
            await module.InvokeVoidAsync("download", name, type, stream);
        }
    }

    private void Advance()
    {
        step = (step + 1) % (Steps.Length + 2);
        if (step == 1)
        {
            runs.Clear();
        }

        overlays = CreateOverlays();
    }

    private void OnBadgeClicked(DiagramBadgeClickedEventArgs e)
    {
        if (e.Badge.Kind == "runs")
        {
            runs[e.ElementId] = runs.GetValueOrDefault(e.ElementId, 1) + 1;
            overlays = CreateOverlays();
        }
    }

    private Dictionary<string, DiagramOverlay> CreateOverlays()
    {
        var result = new Dictionary<string, DiagramOverlay>(StringComparer.Ordinal);
        if (step == 0)
        {
            return result;
        }

        for (var i = 0; i < Steps.Length; i++)
        {
            var id = Steps[i];
            var position = i - (step - 1);
            if (position < 0)
            {
                result[id] = new DiagramOverlay
                {
                    States = ["done"],
                    Badges =
                    [
                        new DiagramBadge { Kind = "status", Text = "完了", Tone = DiagramBadgeTone.Success },
                        new DiagramBadge { Kind = "runs", Text = runs.GetValueOrDefault(id, 1).ToString(CultureInfo.InvariantCulture), Tooltip = "もう一度実行" }
                    ]
                };
                result[Outputs[i]] = new DiagramOverlay
                {
                    States = ["flowed"],
                    Badges = [new DiagramBadge { Text = string.Create(CultureInfo.InvariantCulture, $"{(i + 1) * 120} 件") }]
                };
            }
            else if (position == 0)
            {
                result[id] = new DiagramOverlay
                {
                    States = ["running"],
                    Badges = [new DiagramBadge { Kind = "status", Text = "実行中", Tone = DiagramBadgeTone.Info }]
                };
            }
            else
            {
                result[id] = new DiagramOverlay { Style = new DiagramRenderOverride { Opacity = 0.45 } };
            }
        }

        return result;
    }
}
