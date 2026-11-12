namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed class ChangeSet
{
    private const DiagramChangeParts AllParts =
        DiagramChangeParts.Position | DiagramChangeParts.Size | DiagramChangeParts.Ends | DiagramChangeParts.Parent |
        DiagramChangeParts.Members | DiagramChangeParts.Label | DiagramChangeParts.Data | DiagramChangeParts.Order | DiagramChangeParts.Route;

    private readonly HashSet<string> addedIds = new(StringComparer.Ordinal);

    private readonly HashSet<string> deletedIds = new(StringComparer.Ordinal);

    public List<string> Added { get; } = [];

    public List<string> Deleted { get; } = [];

    public Dictionary<string, DiagramChangeParts> Updated { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

    public bool HasChanges => (Added.Count > 0) || (Deleted.Count > 0) || (Updated.Count > 0);

    public static ChangeSet Between(DiagramSnapshot from, DiagramSnapshot to, ChangeSet recorded)
    {
        var result = new ChangeSet();
        result.Targets.UnionWith(recorded.Targets);
        foreach (var id in recorded.Added.Concat(recorded.Deleted).Concat(recorded.Updated.Keys).Distinct(StringComparer.Ordinal))
        {
            var before = from.Find(id);
            var after = to.Find(id);
            if (before is null)
            {
                if (after is not null)
                {
                    result.MarkAdded(id);
                }

                continue;
            }

            if (after is null)
            {
                result.MarkDeleted(id);
                continue;
            }

            var parts = SnapshotEditor.PartsOf(before, after) | (recorded.Updated.GetValueOrDefault(id) & DiagramChangeParts.Order);
            if ((parts != DiagramChangeParts.None) || !SnapshotContent.Same(before, after))
            {
                result.Updated[id] = parts;
            }
        }

        return result;
    }

    public ChangeSet Copy()
    {
        var copy = new ChangeSet();
        copy.Merge(this);
        return copy;
    }

    public void MarkAdded(string id)
    {
        if (deletedIds.Remove(id))
        {
            Deleted.Remove(id);
            Updated[id] = AllParts;
            return;
        }

        if (addedIds.Add(id))
        {
            Added.Add(id);
        }
    }

    public void MarkDeleted(string id)
    {
        Updated.Remove(id);
        if (addedIds.Remove(id))
        {
            Added.Remove(id);
        }
        else if (deletedIds.Add(id))
        {
            Deleted.Add(id);
        }
    }

    public void MarkUpdated(string id, DiagramChangeParts parts)
    {
        if (addedIds.Contains(id))
        {
            return;
        }

        Updated[id] = Updated.GetValueOrDefault(id) | parts;
    }

    public void Merge(ChangeSet next)
    {
        Targets.UnionWith(next.Targets);
        foreach (var id in next.Deleted)
        {
            MarkDeleted(id);
        }

        foreach (var id in next.Added)
        {
            MarkAdded(id);
        }

        foreach (var (id, parts) in next.Updated)
        {
            MarkUpdated(id, parts);
        }
    }

    public DiagramChangedEventArgs ToArgs(DiagramSnapshot before, DiagramSnapshot after, DiagramChangeOrigin origin, object? tag) =>
        new(before, after, origin, tag, [.. Added], [.. Deleted], Updated.ToImmutableDictionary(StringComparer.Ordinal));
}
