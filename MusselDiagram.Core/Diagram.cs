namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public sealed class Diagram
{
    public Diagram(DiagramSchema schema)
        : this(schema, DiagramSnapshot.Empty)
    {
    }

    public Diagram(DiagramSchema schema, DiagramSnapshot snapshot)
    {
        Index = new SchemaIndex(schema);
        Snapshot = snapshot;
    }

    public event EventHandler<DiagramChangedEventArgs>? Changed;

    public DiagramSchema Schema
    {
        get => Index.Schema;
        set => Index = new SchemaIndex(value);
    }

    public DiagramSnapshot Snapshot { get; private set; }

    internal SchemaIndex Index { get; private set; }

    public DiagramElement? Find(string id) => Snapshot.Find(id);

    public void Add(DiagramElement element, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Add(element));

    public void Connect(DiagramLink link, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Add(link));

    public void Delete(IEnumerable<string> ids, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Delete(ids));

    public void Replace(DiagramElement element, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Replace(element));

    public void Move(IEnumerable<string> ids, DiagramPoint offset, DiagramWriteOptions? options = null) =>
        Move(ids, offset, options, DiagramChangeOrigin.Api);

    public ImmutableArray<DiagramPort> GetPorts(string id) =>
        Snapshot.Find(id) is DiagramBox box ? LinkRouting.GetPorts(box, Index.PortsOf(Index.FindBoxType(box))) : [];

    public DiagramLinkGeometry? GetLinkGeometry(string id) =>
        (Snapshot.Find(id) is DiagramLink { Route.Length: >= 2 } link)
            ? new DiagramLinkGeometry(link.Route, link.Route[0], link.Route[^1], LinkRouting.ToPath(link.Route))
            : null;

    internal void Move(IEnumerable<string> ids, DiagramPoint offset, DiagramWriteOptions? options, DiagramChangeOrigin origin) =>
        Write(options, origin, editor => editor.Move(ids, offset));

    private void Write(DiagramWriteOptions? options, DiagramChangeOrigin origin, Action<SnapshotEditor> edit)
    {
        var before = Snapshot;
        var editor = new SnapshotEditor(before, Index);
        edit(editor);
        if (!editor.HasChanges)
        {
            return;
        }

        var after = editor.Build(before.Version + 1);
        Snapshot = after;
        Changed?.Invoke(this, editor.CreateArgs(before, after, origin, options?.Tag));
    }
}
