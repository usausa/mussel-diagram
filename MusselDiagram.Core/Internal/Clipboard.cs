namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record ClipboardContent(ImmutableArray<DiagramElement> Elements, ImmutableArray<string> Roots);

internal sealed record PasteResult(ImmutableArray<DiagramElement> Elements, ImmutableDictionary<string, string> IdMap, ImmutableArray<string> Selection);

internal static class Clipboard
{
    public static ClipboardContent? Copy(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection)
    {
        var selected = selection.Distinct(StringComparer.Ordinal).Select(snapshot.Find).OfType<DiagramBox>().ToList();
        var boxes = new HashSet<string>(StringComparer.Ordinal);
        var roots = new List<string>();
        foreach (var box in selected)
        {
            if (box is DiagramGroup { Kind: DiagramGroupKind.Overlay })
            {
                continue;
            }

            roots.Add(box.Id);
            boxes.Add(box.Id);
            if (box is DiagramGroup)
            {
                foreach (var descendant in snapshot.GetDescendants(box.Id))
                {
                    boxes.Add(descendant.Id);
                }
            }
        }

        if (boxes.Count == 0)
        {
            return null;
        }

        var overlays = new List<DiagramGroup>();
        foreach (var group in selected.OfType<DiagramGroup>().Where(static x => x.Kind == DiagramGroupKind.Overlay))
        {
            var members = group.MemberIds.Where(boxes.Contains).ToImmutableArray();
            if (members.Length > 0)
            {
                overlays.Add(group with { MemberIds = members });
                roots.Add(group.Id);
            }
        }

        var elements = new List<DiagramElement>();
        elements.AddRange(snapshot.Groups.Where(x => (x.Kind == DiagramGroupKind.Tree) && boxes.Contains(x.Id)));
        elements.AddRange(snapshot.Nodes.Where(x => boxes.Contains(x.Id)));
        elements.AddRange(overlays);
        elements.AddRange(snapshot.Links.Where(x => boxes.Contains(x.Source.ElementId) && boxes.Contains(x.Target.ElementId)));
        var shared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements.ToArray())
        {
            foreach (var id in DiagramRules.References(element, index))
            {
                if ((snapshot.Find(id) is DiagramSharedItem item) && shared.Add(id))
                {
                    elements.Add(item);
                }
            }
        }

        return new ClipboardContent([.. elements], [.. roots]);
    }

    public static (PasteResult? Result, DiagramReason? Reason) Paste(DiagramSnapshot snapshot, SchemaIndex index, ClipboardContent content, DiagramPoint offset)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in content.Elements)
        {
            if (element is not DiagramSharedItem)
            {
                map[element.Id] = DiagramIds.Create();
            }
        }

        var added = new List<DiagramElement>();
        foreach (var element in content.Elements)
        {
            switch (element)
            {
                case DiagramGroup { Kind: DiagramGroupKind.Overlay } group:
                    added.Add(group with { Id = map[group.Id], MemberIds = [.. group.MemberIds.Select(x => map[x])] });
                    break;
                case DiagramBox box:
                    added.Add(box with { Id = map[box.Id], ParentId = ParentOf(box), Bounds = box.Bounds.Offset(offset) });
                    break;
                case DiagramLink link:
                    added.Add(link with
                    {
                        Id = map[link.Id],
                        Source = link.Source with { ElementId = map[link.Source.ElementId] },
                        Target = link.Target with { ElementId = map[link.Target.ElementId] },
                        Route = [],
                        RouteState = DiagramRouteState.Provisional,
                        RouteError = null
                    });
                    break;
                case DiagramSharedItem item when !snapshot.Contains(item.Id):
                    added.Add(item);
                    break;
            }
        }

        DiagramSnapshot trial;
        try
        {
            var editor = new SnapshotEditor(snapshot, index) { SkipRoutes = true };
            foreach (var element in added)
            {
                editor.Add(element);
            }

            editor.Complete();
            trial = editor.Build(snapshot.Version);
        }
        catch (DiagramException)
        {
            return (null, Reason(DiagramReasonKind.Nesting));
        }

        foreach (var type in added.OfType<DiagramBox>().Select(index.FindBoxType).OfType<DiagramBoxType>().DistinctBy(static x => x.Name, StringComparer.Ordinal))
        {
            if ((type.MaxCount is { } max) && (trial.Groups.Cast<DiagramBox>().Concat(trial.Nodes).Count(x => ReferenceEquals(index.FindBoxType(x), type)) > max))
            {
                return (null, Reason(DiagramReasonKind.Count));
            }
        }

        foreach (var box in added.OfType<DiagramBox>())
        {
            if ((box is not DiagramGroup { Kind: DiagramGroupKind.Overlay }) && (DiagramRules.CheckNesting(trial, index, box.Id, box.Type, box.ParentId) is { } reason))
            {
                return (null, reason);
            }
        }

        return (new PasteResult([.. added], map.ToImmutableDictionary(StringComparer.Ordinal), [.. content.Roots.Select(x => map[x])]), null);

        string? ParentOf(DiagramBox box)
        {
            if (box.ParentId is not { } parentId)
            {
                return null;
            }

            if (map.TryGetValue(parentId, out var copied))
            {
                return copied;
            }

            return (snapshot.Find(parentId) is DiagramGroup { Kind: DiagramGroupKind.Tree, Locked: false }) &&
                (DiagramRules.CheckNesting(snapshot, index, null, box.Type, parentId) is null) ? parentId : null;
        }
    }

    private static DiagramReason Reason(DiagramReasonKind kind) => new(kind, DiagramCultures.Reason(kind));
}
