namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public sealed class Diagram
{
    private readonly DiagramHistory history = new();

    private DiagramSnapshot saved;

    private int updateDepth;

    private DiagramSnapshot? updateBefore;

    private ChangeSet? updateChanges;

    private DiagramWriteOptions? updateOptions;

    private DiagramChangeOrigin updateOrigin;

    public Diagram(DiagramSchema schema)
        : this(schema, DiagramSnapshot.Empty)
    {
    }

    public Diagram(DiagramSchema schema, DiagramSnapshot snapshot)
    {
        Index = SchemaIndex.For(schema);
        Snapshot = snapshot;
        saved = snapshot;
    }

    public event EventHandler<DiagramChangedEventArgs>? Changed;

    public DiagramSchema Schema
    {
        get => Index.Schema;
        set => Index = SchemaIndex.For(value);
    }

    public DiagramSnapshot Snapshot { get; private set; }

    public bool CanUndo => (updateDepth == 0) && history.CanUndo;

    public bool CanRedo => (updateDepth == 0) && history.CanRedo;

    public int UndoLimit
    {
        get => history.Limit;
        set => history.Limit = value;
    }

    public bool IsModified => !SnapshotContent.Same(Snapshot, saved);

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

    public void SetParent(IEnumerable<string> ids, string? parentId, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.SetParent(ids, parentId));

    public void SetMembers(string groupId, IEnumerable<string> memberIds, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.SetMembers(groupId, memberIds));

    public IDisposable BeginUpdate(DiagramWriteOptions? options = null) => BeginUpdate(options, DiagramChangeOrigin.Api);

    public void Undo()
    {
        if (CanUndo)
        {
            var step = history.Undo();
            Restore(step.Before, step.Changes, DiagramChangeOrigin.Undo);
        }
    }

    public void Redo()
    {
        if (CanRedo)
        {
            var step = history.Redo();
            Restore(step.After, step.Changes, DiagramChangeOrigin.Redo);
        }
    }

    public void ClearHistory() => history.Clear();

    public ImmutableArray<DiagramIssue> Validate() => DiagramValidator.Validate(Snapshot, Index);

    public void Save(Stream stream) => DiagramJsonFormat.Write(stream, Snapshot, Index);

    public Task SaveAsync(Stream stream, CancellationToken cancellationToken = default) =>
        DiagramJsonFormat.WriteAsync(stream, Snapshot, Index, cancellationToken);

    public string ToJson() => DiagramJsonFormat.ToJson(Snapshot, Index);

    public ImmutableArray<DiagramIssue> Load(Stream stream, DiagramWriteOptions? options = null) =>
        Reset(DiagramJsonFormat.Parse(stream), options);

    public async Task<ImmutableArray<DiagramIssue>> LoadAsync(Stream stream, DiagramWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Reset(await DiagramJsonFormat.ParseAsync(stream, cancellationToken).ConfigureAwait(false), options);

    public ImmutableArray<DiagramIssue> LoadJson(string json, DiagramWriteOptions? options = null) =>
        Reset(DiagramJsonFormat.Parse(json), options);

    public void MarkSaved() => saved = Snapshot;

    public ImmutableArray<DiagramPort> GetPorts(string id) =>
        Snapshot.Find(id) is DiagramBox box ? LinkRouting.GetPorts(box, Index.PortsOf(box)) : [];

    public DiagramLinkGeometry? GetLinkGeometry(string id) =>
        (Snapshot.Find(id) is DiagramLink { Route.Length: >= 2 } link)
            ? new DiagramLinkGeometry(link.Route, link.Route[0], link.Route[^1], LinkRouting.ToPath(link.Route))
            : null;

    internal void Move(IEnumerable<string> ids, DiagramPoint offset, DiagramWriteOptions? options, DiagramChangeOrigin origin) =>
        Write(options, origin, editor => editor.Move(ids, offset));

    internal IDisposable BeginUpdate(DiagramWriteOptions? options, DiagramChangeOrigin origin)
    {
        if (updateDepth == 0)
        {
            updateBefore = Snapshot;
            updateChanges = new ChangeSet();
            updateOptions = options;
            updateOrigin = origin;
        }

        updateDepth++;
        return new UpdateScope(this);
    }

    private void EndUpdate()
    {
        updateDepth--;
        if (updateDepth > 0)
        {
            return;
        }

        var before = updateBefore!;
        var changes = updateChanges!;
        var options = updateOptions;
        updateBefore = null;
        updateChanges = null;
        updateOptions = null;
        if (changes.HasChanges)
        {
            Commit(before, Snapshot, changes, updateOrigin, options);
        }
    }

    private void Write(DiagramWriteOptions? options, DiagramChangeOrigin origin, Action<SnapshotEditor> edit)
    {
        var before = Snapshot;
        var editor = new SnapshotEditor(before, Index);
        edit(editor);
        editor.Complete();
        if (!editor.Changes.HasChanges)
        {
            return;
        }

        if (updateDepth > 0)
        {
            Snapshot = editor.Build(updateBefore!.Version + 1);
            updateChanges!.Merge(editor.Changes);
            return;
        }

        Commit(before, editor.Build(before.Version + 1), editor.Changes, origin, options);
    }

    private void Commit(DiagramSnapshot before, DiagramSnapshot after, ChangeSet changes, DiagramChangeOrigin origin, DiagramWriteOptions? options)
    {
        Snapshot = after;
        history.Add(before, after, changes, options?.History ?? DiagramHistoryMode.Record, Index);
        Changed?.Invoke(this, changes.ToArgs(before, after, origin, options?.Tag));
    }

    private void Restore(DiagramSnapshot target, ChangeSet recorded, DiagramChangeOrigin origin)
    {
        var before = Snapshot;
        var after = target.WithVersion(before.Version + 1);
        Snapshot = after;
        Changed?.Invoke(this, ChangeSet.Between(before, after, recorded).ToArgs(before, after, origin, null));
    }

    private ImmutableArray<DiagramIssue> Reset(DiagramJsonDocument document, DiagramWriteOptions? options)
    {
        var result = DiagramJsonFormat.Read(document, Index, Snapshot.Version + 1);
        var before = Snapshot;
        Snapshot = result.Snapshot;
        history.Clear();
        MarkSaved();
        if (updateDepth > 0)
        {
            updateBefore = Snapshot;
            updateChanges = new ChangeSet();
        }

        Changed?.Invoke(this, new DiagramChangedEventArgs(before, Snapshot, DiagramChangeOrigin.Load, options?.Tag, [], [], []) { IsReset = true });
        return result.Issues;
    }

    private sealed class UpdateScope(Diagram diagram) : IDisposable
    {
        private Diagram? owner = diagram;

        public void Dispose()
        {
            var current = owner;
            owner = null;
            current?.EndUpdate();
        }
    }
}
