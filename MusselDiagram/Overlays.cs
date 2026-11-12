namespace MusselDiagram;

using System.Collections.Immutable;

public enum DiagramBadgeTone
{
    Default,
    Info,
    Success,
    Warning,
    Error
}

public enum DiagramCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public sealed record DiagramOverlay
{
    public ImmutableArray<string> States { get; init; } = [];

    public DiagramRenderOverride? Style { get; init; }

    public ImmutableArray<DiagramBadge> Badges { get; init; } = [];
}

public sealed record DiagramRenderOverride
{
    public string? Fill { get; init; }

    public string? Stroke { get; init; }

    public double? StrokeWidth { get; init; }

    public DiagramDash? Dash { get; init; }

    public double? Opacity { get; init; }
}

public sealed record DiagramBadge
{
    public string? Kind { get; init; }

    public string? Text { get; init; }

    public string? Icon { get; init; }

    public DiagramBadgeTone Tone { get; init; }

    public string? Tooltip { get; init; }
}

public sealed record DiagramBadgeKind
{
    public required string Name { get; init; }

    public DiagramCorner Corner { get; init; } = DiagramCorner.TopRight;

    public int Order { get; init; }

    public bool Clickable { get; init; }
}

public sealed class DiagramBadgeClickedEventArgs : EventArgs
{
    internal DiagramBadgeClickedEventArgs(string elementId, DiagramBadge badge)
    {
        ElementId = elementId;
        Badge = badge;
    }

    public string ElementId { get; }

    public DiagramBadge Badge { get; }
}
