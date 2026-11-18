namespace MusselDiagram;

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
    TooLarge,
    App
}

public sealed record DiagramReason(DiagramReasonKind Kind, string? Message = null);
