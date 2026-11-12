namespace MusselDiagram;

public enum DiagramWheelMode
{
    Pan,
    Zoom
}

public enum DiagramBackgroundDragMode
{
    BoxSelect,
    Pan,
    None
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
    internal DiagramReconnectingEventArgs(string linkId, DiagramLinkEndSide end, DiagramLinkEnd newEnd, DiagramLinkEnd source, DiagramLinkEnd target, string linkType)
    {
        LinkId = linkId;
        End = end;
        NewEnd = newEnd;
        Source = source;
        Target = target;
        LinkType = linkType;
    }

    public string LinkId { get; }

    public DiagramLinkEndSide End { get; }

    public DiagramLinkEnd NewEnd { get; }

    public DiagramLinkEnd Source { get; }

    public DiagramLinkEnd Target { get; }

    public string LinkType { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramReconnectedEventArgs : EventArgs
{
    internal DiagramReconnectedEventArgs(string linkId, DiagramLinkEndSide end, DiagramLinkEnd newEnd, DiagramLinkEnd source, DiagramLinkEnd target, string linkType, DiagramSnapshot snapshot)
    {
        LinkId = linkId;
        End = end;
        NewEnd = newEnd;
        Source = source;
        Target = target;
        LinkType = linkType;
        Snapshot = snapshot;
    }

    public string LinkId { get; }

    public DiagramLinkEndSide End { get; }

    public DiagramLinkEnd NewEnd { get; }

    public DiagramLinkEnd Source { get; }

    public DiagramLinkEnd Target { get; }

    public string LinkType { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramDroppingEventArgs : EventArgs
{
    internal DiagramDroppingEventArgs(string elementId, string type, DiagramRect bounds, string? parentId, string? value, string? splitLinkId)
    {
        ElementId = elementId;
        Type = type;
        Bounds = bounds;
        ParentId = parentId;
        Value = value;
        SplitLinkId = splitLinkId;
    }

    public string ElementId { get; }

    public string Type { get; }

    public DiagramRect Bounds { get; }

    public string? ParentId { get; }

    public string? Value { get; }

    public string? SplitLinkId { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramDroppedEventArgs : EventArgs
{
    internal DiagramDroppedEventArgs(string elementId, string type, DiagramRect bounds, string? parentId, string? value, string? splitLinkId, DiagramSnapshot snapshot)
    {
        ElementId = elementId;
        Type = type;
        Bounds = bounds;
        ParentId = parentId;
        Value = value;
        SplitLinkId = splitLinkId;
        Snapshot = snapshot;
    }

    public string ElementId { get; }

    public string Type { get; }

    public DiagramRect Bounds { get; }

    public string? ParentId { get; }

    public string? Value { get; }

    public string? SplitLinkId { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramDeletingEventArgs : EventArgs
{
    internal DiagramDeletingEventArgs(IReadOnlyList<string> elementIds, bool isCut)
    {
        ElementIds = elementIds;
        IsCut = isCut;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public bool IsCut { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramDeletedEventArgs : EventArgs
{
    internal DiagramDeletedEventArgs(IReadOnlyList<string> elementIds, bool isCut, DiagramSnapshot snapshot)
    {
        ElementIds = elementIds;
        IsCut = isCut;
        Snapshot = snapshot;
    }

    public IReadOnlyList<string> ElementIds { get; }

    public bool IsCut { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramPastingEventArgs : EventArgs
{
    internal DiagramPastingEventArgs(IReadOnlyList<DiagramElement> elements, IReadOnlyDictionary<string, string> idMap)
    {
        Elements = elements;
        IdMap = idMap;
    }

    public IReadOnlyList<DiagramElement> Elements { get; }

    public IReadOnlyDictionary<string, string> IdMap { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramPastedEventArgs : EventArgs
{
    internal DiagramPastedEventArgs(IReadOnlyList<DiagramElement> elements, IReadOnlyDictionary<string, string> idMap, DiagramSnapshot snapshot)
    {
        Elements = elements;
        IdMap = idMap;
        Snapshot = snapshot;
    }

    public IReadOnlyList<DiagramElement> Elements { get; }

    public IReadOnlyDictionary<string, string> IdMap { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramTextCommittingEventArgs : EventArgs
{
    internal DiagramTextCommittingEventArgs(string elementId, string slot, string? oldText, string? newText)
    {
        ElementId = elementId;
        Slot = slot;
        OldText = oldText;
        NewText = newText;
    }

    public string ElementId { get; }

    public string Slot { get; }

    public string? OldText { get; }

    public string? NewText { get; }

    public bool Cancel { get; set; }

    public string? Reason { get; set; }
}

public sealed class DiagramTextCommittedEventArgs : EventArgs
{
    internal DiagramTextCommittedEventArgs(string elementId, string slot, string? oldText, string? newText, DiagramSnapshot snapshot)
    {
        ElementId = elementId;
        Slot = slot;
        OldText = oldText;
        NewText = newText;
        Snapshot = snapshot;
    }

    public string ElementId { get; }

    public string Slot { get; }

    public string? OldText { get; }

    public string? NewText { get; }

    public DiagramSnapshot Snapshot { get; }
}

public sealed class DiagramPointerEventArgs : EventArgs
{
    internal DiagramPointerEventArgs(DiagramPoint position, bool shiftKey, bool ctrlKey, bool altKey, bool metaKey)
    {
        Position = position;
        ShiftKey = shiftKey;
        CtrlKey = ctrlKey;
        AltKey = altKey;
        MetaKey = metaKey;
    }

    public DiagramPoint Position { get; }

    public bool ShiftKey { get; }

    public bool CtrlKey { get; }

    public bool AltKey { get; }

    public bool MetaKey { get; }
}

public sealed class DiagramElementPointerEventArgs : EventArgs
{
    internal DiagramElementPointerEventArgs(string elementId, DiagramPoint position, bool shiftKey, bool ctrlKey, bool altKey, bool metaKey)
    {
        ElementId = elementId;
        Position = position;
        ShiftKey = shiftKey;
        CtrlKey = ctrlKey;
        AltKey = altKey;
        MetaKey = metaKey;
    }

    public string ElementId { get; }

    public DiagramPoint Position { get; }

    public bool ShiftKey { get; }

    public bool CtrlKey { get; }

    public bool AltKey { get; }

    public bool MetaKey { get; }
}
