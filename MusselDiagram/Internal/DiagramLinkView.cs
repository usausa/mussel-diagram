namespace MusselDiagram.Internal;

using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramLinkView : ComponentBase
{
    private DiagramLink? renderedLink;

    private RenderContext? renderedContext;

    [Parameter]
    [EditorRequired]
    public DiagramLink Link { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderContext Context { get; set; } = default!;

    protected override bool ShouldRender() => !ReferenceEquals(Link, renderedLink) || !ReferenceEquals(Context, renderedContext);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedLink = Link;
        renderedContext = Context;
        var link = Link;
        var type = Context.Index.FindLinkType(link.Type);
        var form = type?.Form ?? DiagramLineForm.Orthogonal;
        var (sourceCut, targetCut) = LinkRouting.Cuts(type);

        builder.OpenElement(0, "g");
        builder.AddAttribute(1, "class", type is null ? "mussel-diagram-link mussel-diagram-unregistered" : "mussel-diagram-link");
        builder.AddAttribute(2, "data-id", link.Id);
        builder.AddAttribute(3, "data-type", link.Type);
        if (StyleOf(type?.Look) is { } style)
        {
            builder.AddAttribute(4, "style", style);
        }

        if (Context.Tooltips && (type?.GetTooltip?.Invoke(link) is { Length: > 0 } tooltip))
        {
            builder.OpenElement(5, "title");
            builder.AddContent(6, tooltip);
            builder.CloseElement();
        }

        builder.OpenElement(10, "path");
        builder.AddAttribute(11, "class", "mussel-diagram-link-hit");
        builder.AddAttribute(12, "d", LinkRouting.ToPath(link.Route, form, 0, 0));
        builder.CloseElement();

        builder.OpenElement(20, "path");
        builder.AddAttribute(21, "class", "mussel-diagram-link-line");
        builder.AddAttribute(22, "d", LinkRouting.ToPath(link.Route, form, sourceCut, targetCut));
        var start = type is null ? null : Context.FindMarkerId(type.Name, type.SourceMarker);
        var end = type is null ? Context.DefaultMarkerId : Context.FindMarkerId(type.Name, type.TargetMarker);
        if (start is not null)
        {
            builder.AddAttribute(23, "marker-start", $"url(#{start})");
        }

        if (end is not null)
        {
            builder.AddAttribute(24, "marker-end", $"url(#{end})");
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    private static string? StyleOf(DiagramLook? look)
    {
        if (look is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        if (look.Stroke is not null)
        {
            builder.Append("--mussel-diagram-link-stroke: ").Append(look.Stroke).Append(';');
        }

        if (look.StrokeWidth is { } width)
        {
            builder.Append("--mussel-diagram-link-stroke-width: ").Append(Svg.Number(width)).Append(';');
        }

        if (Svg.Dash(look.Dash) is { } dash)
        {
            builder.Append("--mussel-diagram-link-dash: ").Append(dash).Append(';');
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
