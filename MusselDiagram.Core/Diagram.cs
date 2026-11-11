namespace MusselDiagram;

public sealed class Diagram
{
    public Diagram(DiagramSchema schema)
        : this(schema, DiagramSnapshot.Empty)
    {
    }

    public Diagram(DiagramSchema schema, DiagramSnapshot snapshot)
    {
        Schema = schema;
        Snapshot = snapshot;
    }

    public DiagramSchema Schema { get; set; }

    public DiagramSnapshot Snapshot { get; }
}
