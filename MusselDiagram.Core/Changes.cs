namespace MusselDiagram;

using System.Collections.Immutable;

public sealed record DiagramWriteOptions
{
    public object? Tag { get; init; }
}

public sealed class DiagramChangedEventArgs : EventArgs
{
    private readonly ImmutableDictionary<string, DiagramChangeParts> parts;

    internal DiagramChangedEventArgs(
        DiagramSnapshot before,
        DiagramSnapshot after,
        DiagramChangeOrigin origin,
        object? tag,
        ImmutableArray<string> added,
        ImmutableArray<string> deleted,
        ImmutableDictionary<string, DiagramChangeParts> parts)
    {
        Before = before;
        After = after;
        Origin = origin;
        Tag = tag;
        Added = added;
        Deleted = deleted;
        this.parts = parts;
        Updated = [.. parts.Keys];
    }

    public DiagramSnapshot Before { get; }

    public DiagramSnapshot After { get; }

    public DiagramChangeOrigin Origin { get; }

    public object? Tag { get; }

    public ImmutableArray<string> Added { get; }

    public ImmutableArray<string> Deleted { get; }

    public ImmutableArray<string> Updated { get; }

    public bool IsReset { get; init; }

    public DiagramChangeParts GetChanges(string id) => parts.GetValueOrDefault(id);
}

public sealed class DiagramException : Exception
{
    public DiagramException()
    {
    }

    public DiagramException(string message)
        : base(message)
    {
    }

    public DiagramException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
