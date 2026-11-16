namespace Example.Client.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

public sealed partial class ViewToolbar
{
    private static readonly double[] Levels = [0.25, 0.5, 0.75, 1, 1.5, 2, 4];

    [Parameter]
    public MusselDiagramView? View { get; set; }

    [Parameter]
    public DiagramTool Tool { get; set; }

    [Parameter]
    public EventCallback<DiagramTool> ToolChanged { get; set; }

    [Parameter]
    public DiagramBackgroundDragMode Mode { get; set; }

    [Parameter]
    public EventCallback<DiagramBackgroundDragMode> ModeChanged { get; set; }

    [Parameter]
    public bool SnapToElements { get; set; }

    [Parameter]
    public EventCallback<bool> SnapToElementsChanged { get; set; }

    [Parameter]
    public double Zoom { get; set; } = 1;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private string ZoomText => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(Zoom * 100)}%");

    private bool EditDisabled => (View is null) || View.ReadOnly;

    private static string Percent(double level) => string.Create(CultureInfo.InvariantCulture, $"{level * 100}%");

    private static Color ColorOf(bool pressed) => pressed ? Color.Primary : Color.Default;

    private static string PressedOf(bool pressed) => pressed ? "true" : "false";

    private bool Selects(DiagramBackgroundDragMode mode) => (Tool == DiagramTool.Select) && (mode == Mode);

    private Task PanAsync() => ToolChanged.InvokeAsync(DiagramTool.Pan);

    private Task ToggleSnapAsync() => SnapToElementsChanged.InvokeAsync(!SnapToElements);

    private async Task SelectAsync(DiagramBackgroundDragMode mode)
    {
        await ToolChanged.InvokeAsync(DiagramTool.Select);
        await ModeChanged.InvokeAsync(mode);
    }

    private Task ExecuteAsync(string command) => View?.Commands.ExecuteAsync(command) ?? Task.CompletedTask;

    private async Task SetZoomAsync(double level)
    {
        if (View is not null)
        {
            await View.SetViewportAsync(level, View.Center);
        }
    }

    private async Task ExportSvgAsync()
    {
        if (View is not null)
        {
            await DownloadAsync(await View.ExportSvgAsync(new DiagramSvgExportOptions { IncludeOverlays = true, EmbedFonts = true }), "diagram.svg", "image/svg+xml");
        }
    }

    private async Task ExportImageAsync(DiagramImageFormat format)
    {
        if (View is not null)
        {
            var png = format == DiagramImageFormat.Png;
            var result = await View.ExportImageAsync(new DiagramImageExportOptions { Format = format, Scale = 2, IncludeOverlays = true });
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
            try
            {
                await using var module = await Runtime.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
                await module.InvokeVoidAsync("download", name, type, stream);
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
