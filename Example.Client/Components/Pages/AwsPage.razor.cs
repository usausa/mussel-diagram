namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

public sealed partial class AwsPage
{
    private const long MaxPackageSize = 256L * 1024 * 1024;

    private readonly Diagram diagram = AwsDiagrams.CreateSample(AwsDiagrams.CreateSchema(null));

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private double zoom = 1;

    [Inject]
    private IAwsIconPackage Package { get; set; } = default!;

    [CascadingParameter]
    private GridMode Grid { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            StateHasChanged();
            await view.ZoomToFitAsync();
            var paths = await Package.ListAsync(CancellationToken.None);
            if (paths.Count > 0)
            {
                Apply(DiagramIconImport.FromFiles(paths, Package.ReadAsync, AwsDiagrams.Prefix, AwsDiagrams.Map));
            }
        }
    }

    private async Task ImportAsync(InputFileChangeEventArgs e)
    {
        using var buffer = new MemoryStream();
        var source = e.File.OpenReadStream(MaxPackageSize);
        await using (source.ConfigureAwait(true))
        {
            await source.CopyToAsync(buffer).ConfigureAwait(true);
        }

        buffer.Position = 0;
        Apply(DiagramIconImport.FromZip(buffer, AwsDiagrams.Prefix, AwsDiagrams.Map).IconSet);
    }

    private void Apply(DiagramIconSet icons) => diagram.Schema = AwsDiagrams.CreateSchema(icons);

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
