namespace MusselDiagram.Internal;

using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramBoxView : ComponentBase
{
    private DiagramBox? renderedBox;

    private RenderContext? renderedContext;

    private int renderedTemplates = -1;

    private ElementLook? renderedLook;

    [Parameter]
    [EditorRequired]
    public DiagramBox Box { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderContext Context { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public DiagramTemplateSet Templates { get; set; } = default!;

    [Parameter]
    public ElementLook? Look { get; set; }

    protected override bool ShouldRender() =>
        !ReferenceEquals(Box, renderedBox) || !ReferenceEquals(Context, renderedContext) || (Templates.Version != renderedTemplates) || !ReferenceEquals(Look, renderedLook);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedBox = Box;
        renderedContext = Context;
        renderedTemplates = Templates.Version;
        renderedLook = Look;
        var box = Box;
        var type = Context.Index.FindBoxType(box);
        var look = type?.Look;
        var width = box.Bounds.Width;
        var height = box.Bounds.Height;
        var isGroup = box is DiagramGroup;
        var template = (box is DiagramNode) && (type is not null) ? Templates.Find(box.Type) : null;

        builder.OpenElement(0, "g");
        builder.AddAttribute(1, "class", ClassOf(box, type, Look));
        builder.AddAttribute(2, "data-id", box.Id);
        builder.AddAttribute(3, "data-type", box.Type);
        builder.AddAttribute(4, "transform", Svg.Translate(box.Bounds.X, box.Bounds.Y));
        if (StyleOf(look, Look?.Style) is { } style)
        {
            builder.AddAttribute(5, "style", style);
        }

        if (template is not null)
        {
            builder.AddAttribute(6, "data-template", string.Empty);
        }

        if (Context.Tooltips && (TooltipOf(box, type, Look) is { } tooltip))
        {
            builder.OpenElement(7, "title");
            builder.AddContent(8, tooltip);
            builder.CloseElement();
        }

        if (template is not null)
        {
            builder.OpenComponent<DiagramTemplateView>(9);
            builder.AddComponentParameter(70, nameof(DiagramTemplateView.Node), box);
            builder.AddComponentParameter(71, nameof(DiagramTemplateView.Template), template);
            builder.AddComponentParameter(72, nameof(DiagramTemplateView.Version), Templates.Version);
            builder.CloseComponent();
            AddPorts(builder, box, width, height);
            builder.CloseElement();
            return;
        }

        AddShape(builder, look?.Shape ?? DiagramShape.Rectangle, look?.Path, width, height);

        var icon = box.Icon ?? look?.Icon;
        var iconId = icon is null ? null : Context.FindIconId(icon);
        var iconUrl = (icon is not null) && (iconId is null) && IsUrl(icon) ? icon : null;
        var iconSize = isGroup ? LabelLayout.HeaderIconSize : 0;
        if (iconId is not null)
        {
            builder.OpenElement(20, "use");
            builder.AddAttribute(21, "class", "mussel-diagram-icon");
            builder.AddAttribute(22, "href", $"#{iconId}");
            builder.AddAttribute(23, "width", Svg.Number(isGroup ? iconSize : width));
            builder.AddAttribute(24, "height", Svg.Number(isGroup ? iconSize : height));
            builder.CloseElement();
        }
        else if (iconUrl is not null)
        {
            builder.OpenElement(25, "image");
            builder.AddAttribute(26, "class", "mussel-diagram-icon");
            builder.AddAttribute(27, "href", iconUrl);
            builder.AddAttribute(28, "width", Svg.Number(isGroup ? iconSize : width));
            builder.AddAttribute(29, "height", Svg.Number(isGroup ? iconSize : height));
            builder.AddAttribute(30, "preserveAspectRatio", "xMidYMid meet");
            builder.CloseElement();
        }

        var shown = type is null ? box with { Label = string.IsNullOrEmpty(box.Label) ? box.Type : box.Label } : box;
        var editable = (type is not null) && ((ScriptTables.PermissionsOf(box, Context.Index) & DiagramPermissions.EditText) != 0);
        foreach (var label in LabelLayout.Arrange(shown, type, isGroup && ((iconId is not null) || (iconUrl is not null)), editable))
        {
            builder.OpenElement(40, "text");
            builder.AddAttribute(41, "class", "mussel-diagram-label");
            builder.AddAttribute(50, "data-slot", label.Slot);
            if (label.IsDefault)
            {
                builder.AddAttribute(51, "data-default", string.Empty);
            }

            builder.AddAttribute(42, "x", Svg.Number(label.X));
            builder.AddAttribute(43, "y", Svg.Number(label.Y));
            builder.AddAttribute(44, "text-anchor", label.Anchor);
            builder.AddAttribute(45, "data-label", label.Text);
            builder.AddAttribute(46, "data-width", Svg.Number(label.Width));
            builder.AddAttribute(47, "data-lines", label.MaxLines.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(48, "data-valign", label.VerticalAlign);
            if (TextStyleOf(label.Format) is { } textStyle)
            {
                builder.AddAttribute(49, "style", textStyle);
            }

            builder.CloseElement();
        }

        AddPorts(builder, box, width, height);
        builder.CloseElement();
    }

    private void AddPorts(RenderTreeBuilder builder, DiagramBox box, double width, double height)
    {
        var local = new DiagramRect(0, 0, width, height);
        foreach (var port in Context.Index.PortsOf(box))
        {
            var position = port.PositionIn(local);
            builder.OpenElement(60, "circle");
            builder.AddAttribute(61, "class", "mussel-diagram-port");
            builder.AddAttribute(62, "data-port", port.Id);
            builder.AddAttribute(63, "cx", Svg.Number(position.X));
            builder.AddAttribute(64, "cy", Svg.Number(position.Y));
            builder.AddAttribute(65, "r", "4");
            if (port.Definition.Visibility == DiagramPortVisibility.WhileConnecting)
            {
                builder.AddAttribute(66, "data-hidden", string.Empty);
            }

            if (Context.Tooltips && !string.IsNullOrEmpty(port.Definition.Label))
            {
                builder.OpenElement(67, "title");
                builder.AddContent(68, port.Definition.Label);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }

    private static void AddShape(RenderTreeBuilder builder, DiagramShape shape, string? path, double width, double height)
    {
        switch (shape)
        {
            case DiagramShape.Ellipse:
                builder.OpenElement(10, "ellipse");
                builder.AddAttribute(11, "class", "mussel-diagram-shape");
                builder.AddAttribute(12, "cx", Svg.Number(width / 2));
                builder.AddAttribute(13, "cy", Svg.Number(height / 2));
                builder.AddAttribute(14, "rx", Svg.Number(width / 2));
                builder.AddAttribute(15, "ry", Svg.Number(height / 2));
                builder.CloseElement();
                break;
            case DiagramShape.Diamond:
                builder.OpenElement(10, "polygon");
                builder.AddAttribute(11, "class", "mussel-diagram-shape");
                builder.AddAttribute(16, "points", $"{Svg.Number(width / 2)},0 {Svg.Number(width)},{Svg.Number(height / 2)} {Svg.Number(width / 2)},{Svg.Number(height)} 0,{Svg.Number(height / 2)}");
                builder.CloseElement();
                break;
            case DiagramShape.Path when path is not null:
                builder.OpenElement(10, "path");
                builder.AddAttribute(11, "class", "mussel-diagram-shape mussel-diagram-shape-path");
                builder.AddAttribute(17, "d", path);
                builder.AddAttribute(18, "transform", $"scale({Svg.Number(width)},{Svg.Number(height)})");
                builder.CloseElement();
                break;
            default:
                builder.OpenElement(10, "rect");
                builder.AddAttribute(11, "class", "mussel-diagram-shape");
                builder.AddAttribute(12, "width", Svg.Number(width));
                builder.AddAttribute(13, "height", Svg.Number(height));
                builder.CloseElement();
                break;
        }
    }

    private static string ClassOf(DiagramBox box, DiagramBoxType? type, ElementLook? look)
    {
        var builder = new StringBuilder(box is DiagramGroup ? "mussel-diagram-group" : "mussel-diagram-node");
        if (type is null)
        {
            builder.Append(" mussel-diagram-unregistered");
        }

        if (box.Locked)
        {
            builder.Append(" mussel-diagram-locked");
        }

        AppendLook(builder, look);
        return builder.ToString();
    }

    private static string? StyleOf(DiagramLook? look, DiagramRenderOverride? style)
    {
        var builder = new StringBuilder();
        if (look is not null)
        {
            Append(builder, "--mussel-diagram-shape-fill", look.Fill);
            Append(builder, "--mussel-diagram-shape-stroke", look.Stroke);
            Append(builder, "--mussel-diagram-shape-stroke-width", look.StrokeWidth is { } strokeWidth ? Svg.Number(strokeWidth) : null);
            Append(builder, "--mussel-diagram-shape-dash", Svg.Dash(look.Dash));
        }

        AppendOverride(builder, style);
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? TooltipOf(DiagramBox box, DiagramBoxType? type, ElementLook? look)
    {
        var own = type?.GetTooltip?.Invoke(box);
        if (look?.IssueText is not { } issues)
        {
            return string.IsNullOrEmpty(own) ? null : own;
        }

        var head = string.IsNullOrEmpty(own) ? box.Label : own;
        return string.IsNullOrEmpty(head) ? issues : $"{head}\n{issues}";
    }

    internal static void AppendLook(StringBuilder builder, ElementLook? look)
    {
        if (look is null)
        {
            return;
        }

        foreach (var state in look.States)
        {
            builder.Append(" mussel-diagram-state-").Append(state);
        }

        if (look.Severity is { } severity)
        {
            builder.Append(severity == DiagramIssueSeverity.Error ? " mussel-diagram-error" : " mussel-diagram-warning");
        }
    }

    internal static void AppendOverride(StringBuilder builder, DiagramRenderOverride? style)
    {
        if (style is null)
        {
            return;
        }

        Append(builder, "--mussel-diagram-override-fill", style.Fill);
        Append(builder, "--mussel-diagram-override-stroke", style.Stroke);
        Append(builder, "--mussel-diagram-override-stroke-width", style.StrokeWidth is { } strokeWidth ? Svg.Number(strokeWidth) : null);
        Append(builder, "--mussel-diagram-override-dash", style.Dash is { } dash ? Svg.Dash(dash) ?? "none" : null);
        Append(builder, "--mussel-diagram-override-opacity", style.Opacity is { } opacity ? Svg.Number(opacity) : null);
    }

    private static string? TextStyleOf(DiagramTextFormat? format)
    {
        if (format is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        Append(builder, "font-family", format.FontFamily);
        Append(builder, "font-size", format.FontSize is { } size ? $"{Svg.Number(size)}px" : null);
        Append(builder, "font-weight", format.FontWeight);
        Append(builder, "fill", format.Color);
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static void Append(StringBuilder builder, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        builder.Append(name).Append(": ").Append(value).Append(';');
    }

    private static bool IsUrl(string value) =>
        value.Contains('/', StringComparison.Ordinal) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
}
