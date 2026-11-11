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
    internal DiagramMovingEventArgs(IReadOnlyList<string> elementIds, DiagramPoint offset, string? newParentId)
    {
        ElementIds = elementIds;
        Offset = offset;
        NewParentId = newParentId;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public DiagramPoint Offset { get; }

    public string? NewParentId { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramMovedEventArgs : EventArgs
{
    internal DiagramMovedEventArgs(IReadOnlyList<string> elementIds, DiagramPoint offset, string? newParentId, DiagramSnapshot snapshot)
    {
        ElementIds = elementIds;
        Offset = offset;
        NewParentId = newParentId;
        Snapshot = snapshot;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public DiagramPoint Offset { get; }

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
