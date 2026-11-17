namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal static class Deletion
{
    public static List<string> Deletable(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection) => [.. Find(snapshot, index, selection)];

    public static bool Any(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection) => Find(snapshot, index, selection).Any();

    private static IEnumerable<string> Find(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection)
    {
        var blocked = new Dictionary<string, bool>(StringComparer.Ordinal);
        var stack = new Stack<(string Id, bool Expanded)>();
        foreach (var id in selection.Distinct(StringComparer.Ordinal))
        {
            if ((snapshot.Find(id) is not { } element) || (element is DiagramSharedItem) || !CanDelete(element))
            {
                continue;
            }

            if ((element is DiagramGroup { Kind: DiagramGroupKind.Tree }) && Blocked(id))
            {
                continue;
            }

            yield return id;
        }

        bool CanDelete(DiagramElement element) =>
            ElementPermissions.Allows(element, index, DiagramPermissions.Delete) && !((element is DiagramBox box) && (index.FindBoxType(box) is { Undeletable: true }));

        bool Blocked(string root)
        {
            stack.Push((root, false));
            while (stack.Count > 0)
            {
                var (id, expanded) = stack.Pop();
                if (blocked.ContainsKey(id))
                {
                    continue;
                }

                if (!snapshot.TryGetChildren(id, out var children))
                {
                    blocked[id] = false;
                    continue;
                }

                if (!expanded)
                {
                    stack.Push((id, true));
                    foreach (var child in children)
                    {
                        stack.Push((child.Id, false));
                    }

                    continue;
                }

                var found = false;
                for (var i = 0; (i < children.Count) && !found; i++)
                {
                    found = !CanDelete(children[i]) || blocked[children[i].Id];
                }

                blocked[id] = found;
            }

            return blocked[root];
        }
    }

    public static ImmutableArray<string> Removed(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> ids)
    {
        var editor = new SnapshotEditor(snapshot, index) { SkipRoutes = true };
        editor.Delete(ids);
        editor.Complete();
        return [.. editor.Changes.Deleted];
    }
}
