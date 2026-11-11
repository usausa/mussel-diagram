namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal static class Deletion
{
    public static List<string> Deletable(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection)
    {
        var result = new List<string>();
        foreach (var id in selection.Distinct(StringComparer.Ordinal))
        {
            if ((snapshot.Find(id) is not { } element) || (element is DiagramSharedItem) || !CanDelete(element))
            {
                continue;
            }

            if ((element is DiagramGroup { Kind: DiagramGroupKind.Tree }) && snapshot.GetDescendants(id).Any(x => !CanDelete(x)))
            {
                continue;
            }

            result.Add(id);
        }

        return result;

        bool CanDelete(DiagramElement element) =>
            ElementPermissions.Allows(element, index, DiagramPermissions.Delete) && !((element is DiagramBox box) && (index.FindBoxType(box) is { Undeletable: true }));
    }

    public static ImmutableArray<string> Removed(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> ids)
    {
        var editor = new SnapshotEditor(snapshot, index) { SkipRoutes = true };
        editor.Delete(ids);
        editor.Complete();
        return [.. editor.Changes.Deleted];
    }
}
