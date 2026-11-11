namespace MusselDiagram.Internal;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramDefsView : ComponentBase
{
    private RenderContext? rendered;

    [Parameter]
    [EditorRequired]
    public RenderContext Context { get; set; } = default!;

    protected override bool ShouldRender() => !ReferenceEquals(Context, rendered);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        rendered = Context;
        builder.OpenElement(0, "defs");
        foreach (var marker in Context.Markers)
        {
            builder.OpenElement(1, "marker");
            builder.SetKey(marker.Id);
            builder.AddAttribute(2, "id", marker.Id);
            builder.AddAttribute(3, "class", marker.Kind == DiagramMarker.OpenArrow ? "mussel-diagram-marker mussel-diagram-marker-open" : "mussel-diagram-marker");
            builder.AddAttribute(4, "viewBox", "0 0 10 10");
            builder.AddAttribute(5, "refX", "0");
            builder.AddAttribute(6, "refY", "5");
            builder.AddAttribute(7, "markerWidth", "10");
            builder.AddAttribute(8, "markerHeight", "10");
            builder.AddAttribute(9, "markerUnits", "userSpaceOnUse");
            builder.AddAttribute(10, "orient", "auto-start-reverse");
            if (marker.Color is not null)
            {
                builder.AddAttribute(11, "style", $"--mussel-diagram-marker-color: {marker.Color}");
            }

            if (marker.Kind == DiagramMarker.Circle)
            {
                builder.OpenElement(12, "circle");
                builder.AddAttribute(13, "cx", "5.5");
                builder.AddAttribute(14, "cy", "5");
                builder.AddAttribute(15, "r", "4");
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(16, "path");
                builder.AddAttribute(17, "d", marker.Kind switch
                {
                    DiagramMarker.Diamond => "M0,5L5,0L10,5L5,10Z",
                    DiagramMarker.OpenArrow => "M1,1L10,5L1,9",
                    _ => "M0,0L10,5L0,10Z"
                });
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        foreach (var icon in Context.Icons)
        {
            builder.OpenElement(20, "symbol");
            builder.SetKey(icon.Id);
            builder.AddAttribute(21, "id", icon.Id);
            builder.AddAttribute(22, "viewBox", $"0 0 {Svg.Number(icon.Icon.Width)} {Svg.Number(icon.Icon.Height)}");
            builder.AddMarkupContent(23, icon.Icon.Body);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
