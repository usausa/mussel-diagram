namespace MusselDiagram;

public sealed class DiagramSnapshot
{
    private DiagramSnapshot(long version)
    {
        Version = version;
    }

    public long Version { get; }

    internal static DiagramSnapshot Empty { get; } = new(0);
}
