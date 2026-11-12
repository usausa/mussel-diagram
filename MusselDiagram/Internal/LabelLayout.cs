namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record LabelBox(string Slot, string Text, bool IsDefault, double X, double Y, string Anchor, string VerticalAlign, double Width, int MaxLines, DiagramTextFormat? Format, string? Stack = null);

internal static class LabelLayout
{
    public const double Padding = 6;

    public const double HeaderIconSize = 24;

    private static readonly DiagramLabelSlot NodeSlot = new() { Name = "label", Placement = DiagramLabelPlacement.Center };

    private static readonly DiagramLabelSlot GroupSlot = new() { Name = "label", Placement = DiagramLabelPlacement.Header, MaxLines = 1 };

    public static ImmutableArray<DiagramLabelSlot> SlotsOf(DiagramBox box, DiagramBoxType? type) =>
        (type is null) || type.Slots.IsEmpty ? [box is DiagramGroup ? GroupSlot : NodeSlot] : type.Slots;

    public static ImmutableArray<DiagramLabelSlot> SlotsOf(DiagramBoxType type, bool isGroup) =>
        type.Slots.IsEmpty ? [isGroup ? GroupSlot : NodeSlot] : type.Slots;

    public static ImmutableArray<LabelBox> Arrange(DiagramBox box, DiagramBoxType? type, bool hasHeaderIcon, bool editable)
    {
        var slots = SlotsOf(box, type);
        var builder = ImmutableArray.CreateBuilder<LabelBox>(slots.Length);
        var placements = new List<DiagramLabelPlacement>(slots.Length);
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var text = i == 0 ? box.Label : box.Texts.GetValueOrDefault(slot.Name);
            var isDefault = false;
            if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(slot.DefaultText))
            {
                text = slot.DefaultText;
                isDefault = true;
            }

            if (string.IsNullOrEmpty(text) && !(editable && slot.Editable))
            {
                continue;
            }

            builder.Add(Place(text ?? string.Empty, isDefault, slot, box.Bounds.Width, box.Bounds.Height, hasHeaderIcon, (type as DiagramGroupType)?.Header));
            placements.Add(slot.Placement);
        }

        for (var i = 0; i < builder.Count; i++)
        {
            var placement = placements[i];
            if (placements.Count(x => x == placement) > 1)
            {
                builder[i] = builder[i] with { Stack = placement.ToString() };
            }
        }

        return builder.ToImmutable();
    }

    private static LabelBox Place(string text, bool isDefault, DiagramLabelSlot slot, double width, double height, bool hasHeaderIcon, DiagramGroupHeader? header)
    {
        var (x, y, anchor, align) = slot.Placement switch
        {
            DiagramLabelPlacement.TopLeft => (Padding, Padding, "start", "top"),
            DiagramLabelPlacement.Top => (width / 2, Padding, "middle", "top"),
            DiagramLabelPlacement.TopRight => (width - Padding, Padding, "end", "top"),
            DiagramLabelPlacement.Left => (Padding, height / 2, "start", "middle"),
            DiagramLabelPlacement.Right => (width - Padding, height / 2, "end", "middle"),
            DiagramLabelPlacement.BottomLeft => (Padding, height - Padding, "start", "bottom"),
            DiagramLabelPlacement.Bottom => (width / 2, height - Padding, "middle", "bottom"),
            DiagramLabelPlacement.BottomRight => (width - Padding, height - Padding, "end", "bottom"),
            DiagramLabelPlacement.Above => (width / 2, -4, "middle", "bottom"),
            DiagramLabelPlacement.Below => (width / 2, height + 4, "middle", "top"),
            DiagramLabelPlacement.Before => (-Padding, height / 2, "end", "middle"),
            DiagramLabelPlacement.After => (width + Padding, height / 2, "start", "middle"),
            DiagramLabelPlacement.Header => HeaderPosition(header, hasHeaderIcon, width),
            _ => (width / 2, height / 2, "middle", "middle")
        };
        var inside = slot.Placement is not (DiagramLabelPlacement.Above or DiagramLabelPlacement.Below or DiagramLabelPlacement.Before or DiagramLabelPlacement.After);
        var slotWidth = slot.Width ?? (slot.Placement == DiagramLabelPlacement.Header ? HeaderWidth(anchor, x, width) : inside ? width - (Padding * 2) : width);
        if (slot.Format?.Alignment is { } alignment)
        {
            var left = anchor switch
            {
                "start" => x,
                "end" => x - slotWidth,
                _ => x - (slotWidth / 2)
            };
            (x, anchor) = alignment switch
            {
                DiagramTextAlignment.Start => (left, "start"),
                DiagramTextAlignment.End => (left + slotWidth, "end"),
                _ => (left + (slotWidth / 2), "middle")
            };
        }

        return new LabelBox(slot.Name, text, isDefault, x, y, anchor, align, Math.Max(slotWidth, 1), slot.MaxLines ?? 0, slot.Format);
    }

    private static (double X, double Y, string Anchor, string Align) HeaderPosition(DiagramGroupHeader? header, bool hasIcon, double width)
    {
        var form = header?.Form ?? DiagramHeaderForm.TopLeft;
        var iconSize = header?.IconSize ?? HeaderIconSize;
        var top = header?.NameTop ?? (form == DiagramHeaderForm.TopCenter ? (hasIcon ? iconSize / 2 : 0) + Padding : Padding);
        var alignment = header?.NameAlignment ?? (form == DiagramHeaderForm.TopCenter ? DiagramTextAlignment.Center : DiagramTextAlignment.Start);
        return alignment switch
        {
            DiagramTextAlignment.Center => (width / 2, top, "middle", "top"),
            DiagramTextAlignment.End => (width - Padding, top, "end", "top"),
            _ => (header?.NameLeft ?? ((form == DiagramHeaderForm.TopLeft) && hasIcon ? iconSize + Padding : Padding), top, "start", "top")
        };
    }

    private static double HeaderWidth(string anchor, double x, double width) => anchor switch
    {
        "middle" => width - (Padding * 2),
        "end" => x - Padding,
        _ => width - x - Padding
    };
}
