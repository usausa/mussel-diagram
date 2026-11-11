namespace MusselDiagram;

using System.Collections.Immutable;

public enum DiagramReasonKind
{
    NoConnectionPoint,
    Direction,
    PortKind,
    Permission,
    Limit,
    SelfOrDuplicate,
    PeerType,
    CrossParent,
    Cycle,
    Count,
    Nesting,
    Changed,
    Canceled,
    App
}

public sealed record DiagramReason(DiagramReasonKind Kind, string? Message = null)
{
    public ImmutableArray<string> Arguments { get; init; } = [];
}
