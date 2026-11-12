namespace MusselDiagram.Internal;

using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramLinkView : ComponentBase
{
    private DiagramLink? renderedLink;

    private RenderContext? renderedContext;

    private ElementLook? renderedLook;

    [Parameter]
    [EditorRequired]
    public DiagramLink Link { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderContext Context { get; set; } = default!;

    [Parameter]
    public ElementLook? Look { get; set; }

    protected override bool ShouldRender() => !ReferenceEquals(Link, renderedLink) || !ReferenceEquals(Context, renderedContext) || !ReferenceEquals(Look, renderedLook);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedLink = Link;
        renderedContext = Context;
        renderedLook = Look;
        var link = Link;
        var type = Context.Index.FindLinkType(link.Type);
        var form = type?.Form ?? DiagramLineForm.Orthogonal;
        var (sourceCut, targetCut) = LinkRouting.Cuts(type);

        builder.OpenElement(0, "g");
        var classes = new StringBuilder(type is null ? "mussel-diagram-link mussel-diagram-unregistered" : "mussel-diagram-link");
        DiagramBoxView.AppendLook(classes, Look);
        builder.AddAttribute(1, "class", classes.ToString());
        builder.AddAttribute(2, "data-id", link.Id);
        builder.AddAttribute(3, "data-type", link.Type);
        if (StyleOf(type is null ? null : Context.Index.LookOf(type), Look?.Style) is { } style)
        {
            builder.AddAttribute(4, "style", style);
        }

        if (Context.Tooltips && (TooltipOf(link, type, Look) is { } tooltip))
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

    internal static string? StyleOf(DiagramLook? look, DiagramRenderOverride? style)
    {
        var builder = new StringBuilder();
        if (look?.Stroke is not null)
        {
            builder.Append("--mussel-diagram-link-stroke: ").Append(look.Stroke).Append(';');
        }

        if (look?.StrokeWidth is { } width)
        {
            builder.Append("--mussel-diagram-link-stroke-width: ").Append(Svg.Number(width)).Append(';');
        }

        if ((look is not null) && (Svg.Dash(look.Dash) is { } dash))
        {
            builder.Append("--mussel-diagram-link-dash: ").Append(dash).Append(';');
        }

        DiagramBoxView.AppendOverride(builder, style);
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? TooltipOf(DiagramLink link, DiagramLinkType? type, ElementLook? look)
    {
        var own = type?.GetTooltip?.Invoke(link);
        if (look?.IssueText is not { } issues)
        {
            return string.IsNullOrEmpty(own) ? null : own;
        }

        return string.IsNullOrEmpty(own) ? issues : $"{own}\n{issues}";
    }
}
