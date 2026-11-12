namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramBadgesView : ComponentBase
{
    private ElementBadges? renderedBadges;

    private DiagramElement? renderedElement;

    private RenderContext? renderedContext;

    [Parameter]
    [EditorRequired]
    public DiagramElement Element { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public ElementBadges Badges { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderContext Context { get; set; } = default!;

    public static DiagramPoint Midpoint(ImmutableArray<DiagramPoint> route)
    {
        if (route.IsDefaultOrEmpty)
        {
            return default;
        }

        var total = 0d;
        for (var i = 1; i < route.Length; i++)
        {
            total += Distance(route[i - 1], route[i]);
        }

        var half = total / 2;
        for (var i = 1; i < route.Length; i++)
        {
            var length = Distance(route[i - 1], route[i]);
            if ((length > 0) && (half <= length))
            {
                var t = half / length;
                return new DiagramPoint(route[i - 1].X + ((route[i].X - route[i - 1].X) * t), route[i - 1].Y + ((route[i].Y - route[i - 1].Y) * t));
            }

            half -= length;
        }

        return route[^1];
    }

    protected override bool ShouldRender() =>
        !ReferenceEquals(Badges, renderedBadges) || !ReferenceEquals(Element, renderedElement) || !ReferenceEquals(Context, renderedContext);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedBadges = Badges;
        renderedElement = Element;
        renderedContext = Context;

        builder.OpenElement(0, "g");
        builder.AddAttribute(1, "class", "mussel-diagram-badge-set");
        builder.AddAttribute(2, "data-for", Element.Id);
        if (Element is DiagramBox box)
        {
            builder.AddAttribute(3, "transform", Svg.Translate(box.Bounds.X, box.Bounds.Y));
            builder.AddAttribute(4, "data-w", Svg.Number(box.Bounds.Width));
            builder.AddAttribute(5, "data-h", Svg.Number(box.Bounds.Height));
        }
        else if (Element is DiagramLink link)
        {
            var middle = Midpoint(link.Route);
            builder.AddAttribute(3, "transform", Svg.Translate(middle.X, middle.Y));
            builder.AddAttribute(6, "data-link", string.Empty);
        }

        var items = Badges.Items;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var badge = item.Badge;
            var hasText = !string.IsNullOrEmpty(badge.Text);
            builder.OpenElement(10, "g");
            builder.SetKey(i);
            builder.AddAttribute(11, "class", "mussel-diagram-badge");
            builder.AddAttribute(12, "data-corner", item.Corner);
            builder.AddAttribute(13, "data-index", i.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(14, "data-tone", ToneName(badge.Tone));
            if (item.Clickable)
            {
                builder.AddAttribute(15, "data-clickable", string.Empty);
            }

            if (Context.Tooltips && !string.IsNullOrEmpty(badge.Tooltip))
            {
                builder.OpenElement(16, "title");
                builder.AddContent(17, badge.Tooltip);
                builder.CloseElement();
            }

            builder.OpenElement(20, "rect");
            builder.AddAttribute(21, "class", "mussel-diagram-badge-frame");
            builder.AddAttribute(22, "y", "-8");
            builder.AddAttribute(23, "width", "16");
            builder.AddAttribute(24, "height", "16");
            if (!hasText)
            {
                builder.AddAttribute(25, "rx", "8");
            }

            builder.CloseElement();

            var iconId = badge.Icon is null ? null : Context.FindIconId(badge.Icon);
            if (iconId is not null)
            {
                builder.OpenElement(30, "use");
                builder.AddAttribute(31, "class", "mussel-diagram-badge-icon");
                builder.AddAttribute(32, "href", $"#{iconId}");
                builder.AddAttribute(33, "x", hasText ? "3" : "2");
                builder.AddAttribute(34, "y", "-6");
                builder.AddAttribute(35, "width", "12");
                builder.AddAttribute(36, "height", "12");
                builder.CloseElement();
            }

            if (hasText)
            {
                builder.OpenElement(40, "text");
                builder.AddAttribute(41, "class", "mussel-diagram-badge-text");
                builder.AddAttribute(42, "x", iconId is null ? "4" : "17");
                builder.AddAttribute(43, "y", "0");
                builder.AddContent(44, badge.Text);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static double Distance(DiagramPoint a, DiagramPoint b) => Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    private static string ToneName(DiagramBadgeTone tone) => tone switch
    {
        DiagramBadgeTone.Info => "info",
        DiagramBadgeTone.Success => "success",
        DiagramBadgeTone.Warning => "warning",
        DiagramBadgeTone.Error => "error",
        _ => "default"
    };
}
