namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record LabelBox(string Text, double X, double Y, string Anchor, string VerticalAlign, double Width, int MaxLines, DiagramTextFormat? Format);

internal static class LabelLayout
{
    public const double Padding = 6;

    public const double HeaderIconSize = 24;

    private static readonly DiagramLabelSlot NodeSlot = new() { Name = "label", Placement = DiagramLabelPlacement.Center };

    private static readonly DiagramLabelSlot GroupSlot = new() { Name = "label", Placement = DiagramLabelPlacement.Header, MaxLines = 1 };

    public static ImmutableArray<LabelBox> Arrange(DiagramBox box, DiagramBoxType? type, bool hasHeaderIcon)
    {
        var slots = (type is null) || type.Slots.IsEmpty ? [box is DiagramGroup ? GroupSlot : NodeSlot] : type.Slots;
        var builder = ImmutableArray.CreateBuilder<LabelBox>(slots.Length);
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var text = i == 0 ? box.Label : box.Texts.GetValueOrDefault(slot.Name);
            if (string.IsNullOrEmpty(text))
            {
                text = slot.DefaultText;
            }

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            builder.Add(Place(text, slot, box.Bounds.Width, box.Bounds.Height, hasHeaderIcon));
        }

        return builder.ToImmutable();
    }

    private static LabelBox Place(string text, DiagramLabelSlot slot, double width, double height, bool hasHeaderIcon)
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
            DiagramLabelPlacement.Header => (hasHeaderIcon ? HeaderIconSize + Padding : Padding, Padding, "start", "top"),
            _ => (width / 2, height / 2, "middle", "middle")
        };
        var inside = slot.Placement is not (DiagramLabelPlacement.Above or DiagramLabelPlacement.Below or DiagramLabelPlacement.Before or DiagramLabelPlacement.After);
        var slotWidth = slot.Width ?? (slot.Placement == DiagramLabelPlacement.Header ? width - x - Padding : inside ? width - (Padding * 2) : width);
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

        return new LabelBox(text, x, y, anchor, align, Math.Max(slotWidth, 1), slot.MaxLines ?? 0, slot.Format);
    }
}
