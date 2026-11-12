namespace MusselDiagram.Internal;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class PaletteIcon : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public MusselDiagramView View { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Icon { get; set; } = default!;

    protected override void OnParametersSet() => View.WantIcon(Icon);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "use");
        builder.AddAttribute(1, "href", $"#{View.RenderContext.FindIconId(Icon)}");
        builder.AddAttribute(2, "width", "24");
        builder.AddAttribute(3, "height", "24");
        builder.CloseElement();
    }
}
