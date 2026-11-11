namespace MusselDiagram.Internal;

internal sealed record HistoryStep(DiagramSnapshot Before, DiagramSnapshot After, ChangeSet Changes);

internal sealed class DiagramHistory
{
    private readonly List<HistoryStep> undo = [];

    private readonly List<HistoryStep> redo = [];

    public int Limit { get; set => field = Trim(value); } = 100;

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
    }

    public void Add(DiagramSnapshot before, DiagramSnapshot after, ChangeSet changes, DiagramHistoryMode mode, SchemaIndex index)
    {
        switch (mode)
        {
            case DiagramHistoryMode.Skip:
                Rebase(before, after, changes, index);
                break;
            case DiagramHistoryMode.Merge when (undo.Count > 0) && (redo.Count == 0):
                var last = undo[^1];
                var merged = last.Changes.Copy();
                merged.Merge(changes);
                undo[^1] = new HistoryStep(last.Before, after, merged);
                break;
            default:
                redo.Clear();
                undo.Add(new HistoryStep(before, after, changes));
                Trim(Limit);
                break;
        }
    }

    public HistoryStep Undo()
    {
        var step = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        redo.Add(step);
        return step;
    }

    public HistoryStep Redo()
    {
        var step = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        undo.Add(step);
        return step;
    }

    private static DiagramSnapshot Apply(DiagramSnapshot target, DiagramSnapshot before, DiagramSnapshot after, ChangeSet changes, SchemaIndex index)
    {
        var editor = new SnapshotEditor(target, index) { SkipKindCheck = true };
        editor.Delete(changes.Deleted.Where(target.Contains));

        var added = changes.Added.ToHashSet(StringComparer.Ordinal);
        foreach (var group in after.Groups)
        {
            if (added.Contains(group.Id) && (group.Kind == DiagramGroupKind.Tree))
            {
                TryAdd(group);
            }
        }

        foreach (var node in after.Nodes)
        {
            if (added.Contains(node.Id))
            {
                TryAdd(node);
            }
        }

        foreach (var group in after.Groups)
        {
            if (added.Contains(group.Id) && (group.Kind == DiagramGroupKind.Overlay))
            {
                var members = group.MemberIds.Where(x => editor.Find(x) is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree }).ToArray();
                if (members.Length > 0)
                {
                    TryAdd(group with { MemberIds = [.. members] });
                }
            }
        }

        foreach (var link in after.Links)
        {
            if (added.Contains(link.Id))
            {
                TryAdd(link);
            }
        }

        foreach (var item in after.SharedItems)
        {
            if (added.Contains(item.Id))
            {
                TryAdd(item);
            }
        }

        foreach (var id in changes.Updated.Keys)
        {
            if ((editor.Find(id) is { } current) && (before.Find(id) is { } from) && (after.Find(id) is { } to))
            {
                var patched = Patch(from, to, current, editor);
                if (!ReferenceEquals(patched, current))
                {
                    try
                    {
                        editor.Replace(patched);
                    }
                    catch (DiagramException)
                    {
                    }
                }
            }
        }

        editor.Complete();
        return editor.Changes.HasChanges ? editor.Build(target.Version) : target;

        void TryAdd(DiagramElement element)
        {
            if (editor.Find(element.Id) is not null)
            {
                return;
            }

            try
            {
                editor.Add(element);
            }
            catch (DiagramException)
            {
            }
        }
    }

    private static DiagramElement Patch(DiagramElement from, DiagramElement to, DiagramElement current, SnapshotEditor editor)
    {
        switch (current)
        {
            case DiagramBox box when (from is DiagramBox a) && (to is DiagramBox b):
                var bounds = box.Bounds;
                if (new DiagramPoint(a.Bounds.X, a.Bounds.Y) != new DiagramPoint(b.Bounds.X, b.Bounds.Y))
                {
                    bounds = bounds with { X = b.Bounds.X, Y = b.Bounds.Y };
                }

                if (new DiagramSize(a.Bounds.Width, a.Bounds.Height) != new DiagramSize(b.Bounds.Width, b.Bounds.Height))
                {
                    bounds = bounds with { Width = b.Bounds.Width, Height = b.Bounds.Height };
                }

                var parentId = Pick(a.ParentId, b.ParentId, box.ParentId);
                if ((parentId is not null) && (editor.Find(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree }))
                {
                    parentId = box.ParentId;
                }

                var patched = box with
                {
                    Type = Pick(a.Type, b.Type, box.Type),
                    Data = PickData(a.Data, b.Data, box.Data),
                    Bounds = bounds,
                    ParentId = parentId,
                    Label = Pick(a.Label, b.Label, box.Label),
                    Texts = SnapshotContent.SameTexts(a.Texts, b.Texts) ? box.Texts : b.Texts,
                    Icon = Pick(a.Icon, b.Icon, box.Icon),
                    Locked = Pick(a.Locked, b.Locked, box.Locked),
                    Permissions = Pick(a.Permissions, b.Permissions, box.Permissions)
                };
                if ((patched is DiagramGroup group) && (a is DiagramGroup groupA) && (b is DiagramGroup groupB) && !groupA.MemberIds.SequenceEqual(groupB.MemberIds, StringComparer.Ordinal))
                {
                    var members = groupB.MemberIds.Where(x => editor.Find(x) is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree }).ToArray();
                    if (members.Length > 0)
                    {
                        patched = group with { MemberIds = [.. members] };
                    }
                }

                return SnapshotContent.Same(patched, box) ? box : patched;
            case DiagramLink link when (from is DiagramLink a) && (to is DiagramLink b):
                var patchedLink = link with
                {
                    Type = Pick(a.Type, b.Type, link.Type),
                    Data = PickData(a.Data, b.Data, link.Data),
                    Source = (a.Source != b.Source) && (editor.Find(b.Source.ElementId) is DiagramBox) ? b.Source : link.Source,
                    Target = (a.Target != b.Target) && (editor.Find(b.Target.ElementId) is DiagramBox) ? b.Target : link.Target,
                    Locked = Pick(a.Locked, b.Locked, link.Locked),
                    Permissions = Pick(a.Permissions, b.Permissions, link.Permissions)
                };
                return SnapshotContent.Same(patchedLink, link) ? link : patchedLink;
            case DiagramSharedItem item when (from is DiagramSharedItem a) && (to is DiagramSharedItem b):
                var patchedItem = item with
                {
                    Type = Pick(a.Type, b.Type, item.Type),
                    Data = PickData(a.Data, b.Data, item.Data),
                    Label = Pick(a.Label, b.Label, item.Label)
                };
                return SnapshotContent.Same(patchedItem, item) ? item : patchedItem;
            default:
                return current;
        }
    }

    private static T Pick<T>(T from, T to, T current) => EqualityComparer<T>.Default.Equals(from, to) ? current : to;

    private static object? PickData(object? from, object? to, object? current) => SnapshotContent.SameData(from, to) ? current : to;

    private void Rebase(DiagramSnapshot before, DiagramSnapshot after, ChangeSet changes, SchemaIndex index)
    {
        var rebased = new Dictionary<DiagramSnapshot, DiagramSnapshot>(ReferenceEqualityComparer.Instance) { [before] = after };
        RebaseSteps(undo);
        RebaseSteps(redo);

        void RebaseSteps(List<HistoryStep> steps)
        {
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                var step = steps[i];
                var stepBefore = Map(step.Before);
                var stepAfter = Map(step.After);
                if (SnapshotContent.Same(stepBefore, stepAfter))
                {
                    steps.RemoveAt(i);
                }
                else
                {
                    steps[i] = step with { Before = stepBefore, After = stepAfter };
                }
            }
        }

        DiagramSnapshot Map(DiagramSnapshot snapshot)
        {
            if (!rebased.TryGetValue(snapshot, out var result))
            {
                result = Apply(snapshot, before, after, changes, index);
                rebased[snapshot] = result;
            }

            return result;
        }
    }

    private int Trim(int limit)
    {
        var count = Math.Max(limit, 0);
        if (undo.Count > count)
        {
            undo.RemoveRange(0, undo.Count - count);
        }

        return limit;
    }
}
