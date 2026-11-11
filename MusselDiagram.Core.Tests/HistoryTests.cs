namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class HistoryTests
{
    private static readonly DiagramWriteOptions Skip = new() { History = DiagramHistoryMode.Skip };

    private static readonly DiagramWriteOptions Merge = new() { History = DiagramHistoryMode.Merge };

    [Fact]
    public void UndoAndRedoMakeNewVersions()
    {
        var diagram = new Diagram(Schema);
        Assert.False(diagram.CanUndo);
        diagram.Add(Node("a", 0, 0));
        diagram.Move(["a"], new DiagramPoint(10, 0));
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);

        diagram.Undo();

        Assert.Equal(0, X(diagram, "a"));
        Assert.Equal(3, diagram.Snapshot.Version);
        Assert.Equal(DiagramChangeOrigin.Undo, events[^1].Origin);
        Assert.Equal(DiagramChangeParts.Position, events[^1].GetChanges("a"));
        Assert.True(diagram.CanRedo);

        diagram.Redo();

        Assert.Equal(10, X(diagram, "a"));
        Assert.Equal(4, diagram.Snapshot.Version);
        Assert.Equal(DiagramChangeOrigin.Redo, events[^1].Origin);

        diagram.Undo();
        diagram.Undo();

        Assert.Empty(diagram.Snapshot.Nodes);
        Assert.Equal(["a"], events[^1].Deleted);
        Assert.False(diagram.CanUndo);
        diagram.Undo();
        Assert.Equal(6, diagram.Snapshot.Version);
    }

    [Fact]
    public void RecordingClearsRedoButSkipDoesNot()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 200, 0));
        diagram.Undo();

        diagram.Add(Node("c", 400, 0), Skip);

        Assert.True(diagram.CanRedo);
        diagram.Redo();
        Assert.Equal(["a", "b", "c"], diagram.Snapshot.Nodes.Select(x => x.Id));

        diagram.Undo();
        diagram.Add(Node("d", 600, 0));
        Assert.False(diagram.CanRedo);
    }

    [Fact]
    public void SkipChangesReachEveryStep()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Move(["a"], new DiagramPoint(10, 0));

        diagram.Replace(((DiagramNode)diagram.Find("a")!) with { Label = "Web" }, Skip);
        diagram.Undo();

        Assert.Equal(0, X(diagram, "a"));
        Assert.Equal("Web", ((DiagramNode)diagram.Find("a")!).Label);

        diagram.Undo();
        Assert.Empty(diagram.Snapshot.Nodes);
        diagram.Redo();
        Assert.Equal("Web", ((DiagramNode)diagram.Find("a")!).Label);
        Assert.Equal(0, X(diagram, "a"));
    }

    [Fact]
    public void StepsThatSkipChangesEmptyAreDropped()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 200, 0));
        diagram.Move(["b"], new DiagramPoint(0, 50));
        diagram.Move(["a"], new DiagramPoint(0, 50));

        diagram.Delete(["a"], Skip);
        diagram.Undo();
        diagram.Undo();

        Assert.Empty(diagram.Snapshot.Nodes);
        Assert.False(diagram.CanUndo);
    }

    [Fact]
    public void MergeJoinsThePreviousStep()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Move(["a"], new DiagramPoint(10, 0), Merge);

        diagram.Undo();

        Assert.Empty(diagram.Snapshot.Nodes);
        Assert.False(diagram.CanUndo);

        diagram.Redo();
        diagram.Add(Node("b", 0, 0));
        diagram.Undo();
        diagram.Add(Node("c", 0, 0), Merge);
        diagram.Undo();
        Assert.Equal(["a"], diagram.Snapshot.Nodes.Select(x => x.Id));
    }

    [Fact]
    public void UndoLimitDropsTheOldestSteps()
    {
        var diagram = new Diagram(Schema) { UndoLimit = 2 };
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 0, 0));
        diagram.Add(Node("c", 0, 0));

        diagram.Undo();
        diagram.Undo();

        Assert.Equal(["a"], diagram.Snapshot.Nodes.Select(x => x.Id));
        Assert.False(diagram.CanUndo);

        diagram.Redo();
        diagram.UndoLimit = 0;
        Assert.False(diagram.CanUndo);
        diagram.ClearHistory();
        Assert.False(diagram.CanRedo);
    }

    [Fact]
    public void BeginUpdateMakesOneChange()
    {
        var diagram = new Diagram(Schema);
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);
        var tag = new object();

        using (diagram.BeginUpdate(new DiagramWriteOptions { Tag = tag }))
        {
            diagram.Add(Node("a", 0, 0));
            using (diagram.BeginUpdate())
            {
                diagram.Add(Node("b", 200, 0));
                diagram.Connect(Link("ab", "a", "b"));
            }

            diagram.Move(["a"], new DiagramPoint(0, 10));
            Assert.Empty(events);
            Assert.True(diagram.Snapshot.Contains("ab"));
            Assert.False(diagram.CanUndo);
            Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0)));
        }

        var change = Assert.Single(events);
        Assert.Equal(["a", "b", "ab"], change.Added);
        Assert.Same(tag, change.Tag);
        Assert.Equal(1, diagram.Snapshot.Version);
        Assert.Equal(10, ((DiagramBox)diagram.Find("a")!).Bounds.Y);

        diagram.Undo();
        Assert.Empty(diagram.Snapshot.Nodes);
        Assert.Empty(diagram.Snapshot.Links);
    }

    private static double X(Diagram diagram, string id) => ((DiagramBox)diagram.Find(id)!).Bounds.X;
}
