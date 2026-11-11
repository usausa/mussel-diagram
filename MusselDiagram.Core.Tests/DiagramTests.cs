namespace MusselDiagram;

public sealed class DiagramTests
{
    [Fact]
    public void NewDiagramStartsAtTheFirstVersion()
    {
        var schema = new DiagramSchema();

        var diagram = new Diagram(schema);

        Assert.Same(schema, diagram.Schema);
        Assert.Equal(0, diagram.Snapshot.Version);
    }
}
