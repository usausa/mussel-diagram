namespace Example.Client.Components.Layout;

using Microsoft.AspNetCore.Components;

using MudBlazor;

public sealed partial class ViewToolbar
{
    private static readonly double[] Levels = [0.25, 0.5, 0.75, 1, 1.5, 2, 4];

    [Parameter]
    public MusselDiagramView? View { get; set; }

    [Parameter]
    public DiagramBackgroundDragMode Mode { get; set; }

    [Parameter]
    public EventCallback<DiagramBackgroundDragMode> ModeChanged { get; set; }

    [Parameter]
    public double Zoom { get; set; } = 1;

    private string ZoomText => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(Zoom * 100)}%");

    private static string Percent(double level) => string.Create(CultureInfo.InvariantCulture, $"{level * 100}%");

    private Color ColorOf(DiagramBackgroundDragMode mode) => mode == Mode ? Color.Primary : Color.Default;

    private string PressedOf(DiagramBackgroundDragMode mode) => mode == Mode ? "true" : "false";

    private Task SetModeAsync(DiagramBackgroundDragMode mode) => ModeChanged.InvokeAsync(mode);

    private Task ExecuteAsync(string command) => View?.Commands.ExecuteAsync(command) ?? Task.CompletedTask;

    private async Task SetZoomAsync(double level)
    {
        if (View is not null)
        {
            await View.SetViewportAsync(level, View.Center);
        }
    }
}
