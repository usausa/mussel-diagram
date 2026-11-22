namespace MusselDiagram;

using System.Collections.Immutable;
using System.Diagnostics;

using MusselDiagram.Internal;

public sealed class Diagram
{
    private readonly DiagramHistory history = new();

    private readonly Dictionary<string, PendingRoute> routes = new(StringComparer.Ordinal);

    private readonly HashSet<string> deferredLinks = new(StringComparer.Ordinal);

    private readonly Dictionary<string, (DiagramLink Original, DiagramLink Routed, DiagramSnapshot Routing)> routedLinks = new(StringComparer.Ordinal);

    private long unpublishedSince;

    private static readonly DiagramWriteOptions SkipHistory = new() { History = DiagramHistoryMode.Skip };

    private RouteBudget? updateBudget;

    private bool deferredScheduled;

    private DiagramSnapshot saved;

    private int updateDepth;

    private DiagramSnapshot? updateBefore;

    private ChangeSet? updateChanges;

    private DiagramWriteOptions? updateOptions;

    private DiagramChangeOrigin updateOrigin;

    private MusselDiagramOptions? registeredOptions;

    public Diagram(DiagramSchema schema)
        : this(schema, DiagramSnapshot.Empty)
    {
    }

    public Diagram(DiagramSchema schema, DiagramSnapshot snapshot)
    {
        Index = SchemaIndex.For(schema);
        Snapshot = snapshot;
        saved = snapshot;
        DeferProvisional();
    }

    public event EventHandler<DiagramChangedEventArgs>? Changed;

    internal event EventHandler? SchemaChanged;

    public DiagramSchema Schema
    {
        get => Index.Schema;
        set
        {
            Index = SchemaIndex.For(value);
            SchemaChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MusselDiagramOptions? Options { get; init; }

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

    internal MusselDiagramOptions EffectiveOptions => Options ?? registeredOptions ?? MusselDiagramOptions.Default;

    internal static readonly TimeSpan DefaultRouteLimit = TimeSpan.FromMilliseconds(20);

    internal TimeSpan RouteLimit { get; set; } = DefaultRouteLimit;

    internal TimeSpan BackgroundRouteLimit { get; set; } = TimeSpan.FromMilliseconds(4);

    internal TimeSpan PublishInterval { get; set; } = TimeSpan.FromSeconds(1);

    internal static bool CanDefer => (SynchronizationContext.Current is not null) || OperatingSystem.IsBrowser();

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

    public void Resize(string id, DiagramRect bounds, DiagramWriteOptions? options = null) =>
        Resize(id, bounds, options, DiagramChangeOrigin.Api);

    public void Reconnect(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, DiagramWriteOptions? options = null) =>
        Reconnect(linkId, side, end, options, DiagramChangeOrigin.Api);

    public void SetParent(IEnumerable<string> ids, string? parentId, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.SetParent(ids, parentId));

    public void SetMembers(string groupId, IEnumerable<string> memberIds, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.SetMembers(groupId, memberIds));

    public void BringToFront(IEnumerable<string> ids, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Reorder(ids, OrderMove.Front));

    public void SendToBack(IEnumerable<string> ids, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Reorder(ids, OrderMove.Back));

    public void BringForward(IEnumerable<string> ids, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Reorder(ids, OrderMove.Forward));

    public void SendBackward(IEnumerable<string> ids, DiagramWriteOptions? options = null) =>
        Write(options, DiagramChangeOrigin.Api, editor => editor.Reorder(ids, OrderMove.Backward));

    public IDisposable BeginUpdate(DiagramWriteOptions? options = null) => BeginUpdate(options, DiagramChangeOrigin.Api);

    public void Undo() => UndoStep();

    public void Redo() => RedoStep();

    public void ClearHistory() => history.Clear();

    public ImmutableArray<DiagramIssue> Validate() => DiagramValidator.Validate(Snapshot, Index, EffectiveOptions);

    public void Save(Stream stream) => DiagramJsonFormat.Write(stream, Snapshot, Index);

    public Task SaveAsync(Stream stream, CancellationToken cancellationToken = default) =>
        DiagramJsonFormat.WriteAsync(stream, Snapshot, Index, cancellationToken);

    public string ToJson() => DiagramJsonFormat.ToJson(Snapshot, Index);

    public ImmutableArray<DiagramIssue> Load(Stream stream, DiagramWriteOptions? options = null) =>
        Reset(DiagramJsonFormat.Parse(stream), options);

    public async Task<ImmutableArray<DiagramIssue>> LoadAsync(Stream stream, DiagramWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Reset(await DiagramJsonFormat.ParseAsync(stream, cancellationToken).ConfigureAwait(true), options);

    public ImmutableArray<DiagramIssue> LoadJson(string json, DiagramWriteOptions? options = null) =>
        Reset(DiagramJsonFormat.Parse(json), options);

    public void MarkSaved() => saved = Snapshot;

    public ImmutableArray<DiagramPort> GetPorts(string id) =>
        Snapshot.Find(id) is DiagramBox box ? LinkRouting.GetPorts(box, Index.PortsOf(box)) : [];

    public DiagramLinkGeometry? GetLinkGeometry(string id)
    {
        if (Snapshot.Find(id) is not DiagramLink { Route.Length: >= 2 } link)
        {
            return null;
        }

        var type = Index.FindLinkType(link.Type);
        var (sourceCut, targetCut) = LinkRouting.Cuts(type, link.Look);
        var route = link.Route;
        return new DiagramLinkGeometry
        {
            Points = route,
            SourcePoint = route[0],
            TargetPoint = route[^1],
            SourceAngle = LinkRouting.Angle(route[1], route[0]),
            TargetAngle = LinkRouting.Angle(route[^2], route[^1]),
            Path = LinkRouting.ToPath(route, LinkRouting.FormOf(type, link.Look), sourceCut, targetCut)
        };
    }

    internal IReadOnlyCollection<string>? UndoStep()
    {
        if (!CanUndo)
        {
            return null;
        }

        var step = history.Undo();
        Restore(step.Before, step.Changes, DiagramChangeOrigin.Undo);
        return step.Changes.Targets;
    }

    internal IReadOnlyCollection<string>? RedoStep()
    {
        if (!CanRedo)
        {
            return null;
        }

        var step = history.Redo();
        Restore(step.After, step.Changes, DiagramChangeOrigin.Redo);
        return step.Changes.Targets;
    }

    internal void Move(IEnumerable<string> ids, DiagramPoint offset, DiagramWriteOptions? options, DiagramChangeOrigin origin) =>
        Write(options, origin, editor => editor.Move(ids, offset));

    internal void Resize(string id, DiagramRect bounds, DiagramWriteOptions? options, DiagramChangeOrigin origin) =>
        Write(options, origin, editor => editor.Resize(id, bounds));

    internal void Reconnect(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, DiagramWriteOptions? options, DiagramChangeOrigin origin) =>
        Write(options, origin, editor => editor.Reconnect(linkId, side, end));

    internal void Write(DiagramWriteOptions? options, DiagramChangeOrigin origin, Action<SnapshotEditor> edit, bool fromRouter = false)
    {
        var before = Snapshot;
        var editor = new SnapshotEditor(before, Index) { Budget = updateDepth > 0 ? updateBudget : NewBudget(RouteLimit), WaitForRoutes = !CanDefer };
        edit(editor);
        editor.Complete();
        if (!editor.Changes.HasChanges)
        {
            Track(editor.PendingRoutes);
            Defer(editor.DeferredLinks);
            return;
        }

        if (updateDepth > 0)
        {
            Snapshot = editor.Build(updateBefore!.Version + 1);
            updateChanges!.Merge(editor.Changes);
            Track(editor.PendingRoutes);
            Defer(editor.DeferredLinks);
            return;
        }

        var after = editor.Build(before.Version + 1);
        try
        {
            Commit(before, after, editor.Changes, origin, options, fromRouter);
        }
        finally
        {
            Track(editor.PendingRoutes);
            Defer(editor.DeferredLinks);
        }
    }

    internal void UseRegisteredOptions(MusselDiagramOptions? registered) => registeredOptions ??= registered;

    internal IDisposable BeginUpdate(DiagramWriteOptions? options, DiagramChangeOrigin origin)
    {
        if (updateDepth == 0)
        {
            updateBefore = Snapshot;
            updateChanges = new ChangeSet();
            updateOptions = options;
            updateOrigin = origin;
            updateBudget = NewBudget(RouteLimit);
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
        updateBudget = null;
        if (!changes.HasChanges)
        {
            return;
        }

        var net = ChangeSet.Between(before, Snapshot, changes);
        if (!net.HasChanges)
        {
            Snapshot = before;
            return;
        }

        Commit(before, Snapshot, net, updateOrigin, options);
    }

    private void Track(IReadOnlyList<PendingRoute> pending)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var context = SynchronizationContext.Current;
        foreach (var route in pending)
        {
            if (routes.TryGetValue(route.LinkId, out var replaced))
            {
                replaced.Cancellation.Cancel();
            }

            routes[route.LinkId] = route;
            _ = route.Task.ContinueWith(
                task =>
                {
                    if (context is null)
                    {
                        ApplyRoute(route, task);
                    }
                    else
                    {
                        context.Post(_ => ApplyRoute(route, task), null);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private void ApplyRoute(PendingRoute route, Task<DiagramRouteResult> task)
    {
        if (updateDepth > 0)
        {
            if (SynchronizationContext.Current is { } context)
            {
                context.Post(_ => ApplyRoute(route, task), null);
                return;
            }

            if (OperatingSystem.IsBrowser())
            {
                _ = Task.Delay(1).ContinueWith(_ => ApplyRoute(route, task), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return;
            }
        }

        route.Cancellation.Dispose();
        if (!routes.TryGetValue(route.LinkId, out var latest) || !ReferenceEquals(latest, route))
        {
            return;
        }

        routes.Remove(route.LinkId);
        if (Snapshot.Find(route.LinkId) is not DiagramLink { RouteState: DiagramRouteState.Provisional })
        {
            return;
        }

        Write(SkipHistory, DiagramChangeOrigin.Api, editor => editor.CompleteRoute(route, task), true);
    }

    private void Commit(DiagramSnapshot before, DiagramSnapshot after, ChangeSet changes, DiagramChangeOrigin origin, DiagramWriteOptions? options, bool fromRouter = false)
    {
        Snapshot = after;
        if (fromRouter)
        {
            saved = DiagramHistory.ApplyRoutes(saved, before, after, changes, Index);
        }

        history.Add(before, after, changes, options?.History ?? DiagramHistoryMode.Record, Index);
        CancelRemovedRoutes();
        Changed?.Invoke(this, changes.ToArgs(before, after, origin, options?.Tag));
    }

    private void CancelRemovedRoutes()
    {
        if (routes.Count == 0)
        {
            return;
        }

        foreach (var route in routes.Values.ToArray())
        {
            if (Snapshot.Find(route.LinkId) is not DiagramLink)
            {
                routes.Remove(route.LinkId);
                route.Cancellation.Cancel();
            }
        }
    }

    private void Restore(DiagramSnapshot target, ChangeSet recorded, DiagramChangeOrigin origin)
    {
        var before = Snapshot;
        var after = target.WithVersion(before.Version + 1);
        Snapshot = after;
        CancelRemovedRoutes();
        try
        {
            Changed?.Invoke(this, ChangeSet.Restored(before, after, recorded).ToArgs(before, after, origin, null));
        }
        finally
        {
            DeferProvisional();
        }
    }

    private static RouteBudget? NewBudget(TimeSpan limit) => CanDefer ? new RouteBudget(limit) : null;

    private void DeferProvisional()
    {
        var ids = new List<string>();
        var requests = new List<string>();
        foreach (var link in Snapshot.Links)
        {
            if ((link.RouteState != DiagramRouteState.Provisional) || routes.ContainsKey(link.Id))
            {
                continue;
            }

            if ((Index.FindLinkType(link.Type)?.Router ?? Index.Schema.Router) is null)
            {
                ids.Add(link.Id);
            }
            else
            {
                requests.Add(link.Id);
            }
        }

        Defer(ids);
        if (requests.Count > 0)
        {
            Write(SkipHistory, DiagramChangeOrigin.Api, editor => editor.RequestRoutes(requests), true);
        }
    }

    private void Defer(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        deferredLinks.UnionWith(ids);
        Schedule();
    }

    private void Schedule()
    {
        if (deferredScheduled)
        {
            return;
        }

        if (SynchronizationContext.Current is { } context)
        {
            deferredScheduled = true;
            context.Post(_ => RouteDeferred(), null);
        }
        else if (OperatingSystem.IsBrowser())
        {
            deferredScheduled = true;
            _ = Task.Delay(1).ContinueWith(_ => RouteDeferred(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
        else
        {
            RouteDeferred();
        }
    }

    private void RouteDeferred()
    {
        deferredScheduled = false;
        if ((deferredLinks.Count == 0) && (routedLinks.Count == 0))
        {
            return;
        }

        if (updateDepth > 0)
        {
            Schedule();
            return;
        }

        if (routedLinks.Count == 0)
        {
            unpublishedSince = Stopwatch.GetTimestamp();
        }

        var source = Snapshot;
        var editor = new SnapshotEditor(source, Index) { Budget = NewBudget(BackgroundRouteLimit) };
        deferredLinks.ExceptWith(editor.RouteDeferred(deferredLinks));
        foreach (var id in editor.Changes.Updated.Keys)
        {
            if ((source.Find(id) is DiagramLink original) && (editor.Find(id) is DiagramLink { RouteState: not DiagramRouteState.Provisional } routed))
            {
                routedLinks[id] = (original, routed, source);
            }
        }

        if ((deferredLinks.Count == 0) || (Stopwatch.GetElapsedTime(unpublishedSince) > PublishInterval))
        {
            Publish();
        }

        if (deferredLinks.Count > 0)
        {
            Schedule();
        }
    }

    private void Publish()
    {
        if (routedLinks.Count == 0)
        {
            return;
        }

        (DiagramLink Original, DiagramLink Routed, DiagramSnapshot Routing)[] results = [.. routedLinks.Values];
        routedLinks.Clear();
        Write(SkipHistory, DiagramChangeOrigin.Api, editor => editor.ApplyRoutes(results), true);
    }

    private ImmutableArray<DiagramIssue> Reset(DiagramJsonDocument document, DiagramWriteOptions? options)
    {
        var result = DiagramJsonFormat.Read(document, Index, Snapshot.Version + 1, NewBudget(RouteLimit), !CanDefer, EffectiveOptions);
        var before = Snapshot;
        Snapshot = result.Snapshot;
        history.Clear();
        foreach (var route in routes.Values)
        {
            route.Cancellation.Cancel();
        }

        routes.Clear();
        routedLinks.Clear();
        MarkSaved();
        if (updateDepth > 0)
        {
            updateBefore = Snapshot;
            updateChanges = new ChangeSet();
        }

        try
        {
            Changed?.Invoke(this, new DiagramChangedEventArgs(before, Snapshot, DiagramChangeOrigin.Load, options?.Tag, [], [], []) { IsReset = true });
        }
        finally
        {
            Track(result.PendingRoutes);
            deferredLinks.Clear();
            DeferProvisional();
        }

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
