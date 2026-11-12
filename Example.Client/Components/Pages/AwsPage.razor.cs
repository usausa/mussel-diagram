namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components.Forms;

public sealed partial class AwsPage
{
    private const long MaxPackageSize = 256L * 1024 * 1024;

    private readonly Diagram diagram = AwsDiagrams.CreateSample(AwsDiagrams.CreateSchema(null));

    private MusselDiagramView? view;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            StateHasChanged();
            await view.ZoomToFitAsync();
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
        var result = DiagramIconImport.FromZip(buffer, AwsDiagrams.Prefix, AwsDiagrams.Map);
        diagram.Schema = AwsDiagrams.CreateSchema(result.IconSet);
    }
}
