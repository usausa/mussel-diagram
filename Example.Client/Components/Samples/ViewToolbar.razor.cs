namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

public sealed partial class ViewToolbar
{
    private static readonly double[] Levels = [0.25, 0.5, 0.75, 1, 1.5, 2, 4];

    private string? message;

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

    private string ZoomText => String.Create(CultureInfo.InvariantCulture, $"{Math.Round(Zoom * 100)}%");

    private bool EditDisabled => (View is null) || View.ReadOnly;

    private static string Percent(double level) => String.Create(CultureInfo.InvariantCulture, $"{level * 100}%");

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

    private Task ExportSvgAsync() =>
        ExportAsync(static view => view.ExportSvgAsync(new DiagramSvgExportOptions { IncludeOverlays = true, EmbedFonts = true }), "diagram.svg", "image/svg+xml");

    private Task ExportImageAsync(DiagramImageFormat format)
    {
        var png = format == DiagramImageFormat.Png;
        return ExportAsync(view => view.ExportImageAsync(new DiagramImageExportOptions { Format = format, Scale = 2, IncludeOverlays = true }), png ? "diagram.png" : "diagram.jpg", png ? "image/png" : "image/jpeg");
    }

    private async Task ExportAsync(Func<MusselDiagramView, Task<DiagramExportResult>> export, string name, string type)
    {
        if (View is not { } view)
        {
            return;
        }

        try
        {
            await DownloadAsync(await export(view), name, type);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException)
        {
            message = "書き出せませんでした。";
        }
    }

    private async Task DownloadAsync(DiagramExportResult result, string name, string type)
    {
        message = result switch
        {
            { Error: { } error } => error.Message ?? "書き出せませんでした。",
            { Content: null } => "書き出すものがありません。",
            { Issues.IsDefaultOrEmpty: false } => "埋め込めなかった画像があります。",
            _ => null
        };
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
