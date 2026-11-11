namespace MusselDiagram;

public enum DiagramWheelMode
{
    Pan,
    Zoom
}

public enum DiagramCommitMode
{
    Optimistic,
    Wait
}

public enum DiagramGridStyle
{
    Dots,
    Lines
}

public enum DiagramOperation
{
    Move,
    Resize,
    Connect,
    Reconnect,
    Drop,
    Delete,
    TextEdit,
    Paste,
    Command
}

public sealed record DiagramViewport(double Zoom, DiagramPoint Center, DiagramRect VisibleBounds);

public sealed class DiagramMovingEventArgs : EventArgs
{
    internal DiagramMovingEventArgs(IReadOnlyList<string> elementIds, DiagramPoint offset, bool parentChanged, string? newParentId)
    {
        ElementIds = elementIds;
        Offset = offset;
        ParentChanged = parentChanged;
        NewParentId = newParentId;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public DiagramPoint Offset { get; }

    public bool ParentChanged { get; }

    public string? NewParentId { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramMovedEventArgs : EventArgs
{
    internal DiagramMovedEventArgs(IReadOnlyList<string> elementIds, DiagramPoint offset, bool parentChanged, string? newParentId, DiagramSnapshot snapshot)
    {
        ElementIds = elementIds;
        Offset = offset;
        ParentChanged = parentChanged;
        NewParentId = newParentId;
        Snapshot = snapshot;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public DiagramPoint Offset { get; }

    public bool ParentChanged { get; }

    public string? NewParentId { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramCommitRejectedEventArgs : EventArgs
{
    internal DiagramCommitRejectedEventArgs(DiagramOperation operation, DiagramReason reason)
    {
        Operation = operation;
        Reason = reason;
    }

    public DiagramOperation Operation { get; }

    public DiagramReason Reason { get; }
}

public sealed class DiagramResizingEventArgs : EventArgs
{
    internal DiagramResizingEventArgs(string elementId, DiagramRect bounds)
    {
        ElementId = elementId;
        Bounds = bounds;
    }

    public string ElementId { get; }

    public DiagramRect Bounds { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramResizedEventArgs : EventArgs
{
    internal DiagramResizedEventArgs(string elementId, DiagramRect bounds, DiagramSnapshot snapshot)
    {
        ElementId = elementId;
        Bounds = bounds;
        Snapshot = snapshot;
    }

    public string ElementId { get; }

    public DiagramRect Bounds { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramConnectingEventArgs : EventArgs
{
    internal DiagramConnectingEventArgs(DiagramLinkEnd source, DiagramLinkEnd target, string linkType, DiagramBox? placedElement)
    {
        Source = source;
        Target = target;
        LinkType = linkType;
        PlacedElement = placedElement;
    }

    public DiagramLinkEnd Source { get; }

    public DiagramLinkEnd Target { get; }

    public string LinkType { get; }

    public DiagramBox? PlacedElement { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramConnectedEventArgs : EventArgs
{
    internal DiagramConnectedEventArgs(string linkId, DiagramLinkEnd source, DiagramLinkEnd target, string linkType, DiagramBox? placedElement, DiagramSnapshot snapshot)
    {
        LinkId = linkId;
        Source = source;
        Target = target;
        LinkType = linkType;
        PlacedElement = placedElement;
        Snapshot = snapshot;
    }

    public string LinkId { get; }

    public DiagramLinkEnd Source { get; }

    public DiagramLinkEnd Target { get; }

    public string LinkType { get; }

    public DiagramBox? PlacedElement { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramReconnectingEventArgs : EventArgs
{
    internal DiagramReconnectingEventArgs(string linkId, DiagramLinkEndSide end, DiagramLinkEnd newEnd)
    {
        LinkId = linkId;
        End = end;
        NewEnd = newEnd;
    }

    public string LinkId { get; }

    public DiagramLinkEndSide End { get; }

    public DiagramLinkEnd NewEnd { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramReconnectedEventArgs : EventArgs
{
    internal DiagramReconnectedEventArgs(string linkId, DiagramLinkEndSide end, DiagramLinkEnd newEnd, DiagramSnapshot snapshot)
    {
        LinkId = linkId;
        End = end;
        NewEnd = newEnd;
        Snapshot = snapshot;
    }

    public string LinkId { get; }

    public DiagramLinkEndSide End { get; }

    public DiagramLinkEnd NewEnd { get; }

    public DiagramSnapshot Snapshot { get; }
}
