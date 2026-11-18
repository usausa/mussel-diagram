namespace MusselDiagram.Internal;

internal sealed class ScriptChangeQueue
{
    private readonly List<string> order = [];

    private readonly HashSet<string> listed = new(StringComparer.Ordinal);

    private readonly HashSet<string> removed = new(StringComparer.Ordinal);

    public bool IsEmpty => (order.Count == 0) && (removed.Count == 0);

    public void Add(DiagramChangedEventArgs change)
    {
        foreach (var id in change.Deleted)
        {
            removed.Add(id);
            if (listed.Remove(id))
            {
                order.Remove(id);
            }
        }

        foreach (var id in change.Added)
        {
            if (!listed.Add(id))
            {
                order.Remove(id);
            }

            order.Add(id);
        }

        foreach (var id in change.Updated)
        {
            if ((change.GetChanges(id) != DiagramChangeParts.Route) && listed.Add(id))
            {
                order.Add(id);
            }
        }
    }

    public void Clear()
    {
        order.Clear();
        listed.Clear();
        removed.Clear();
    }

    public ScriptChanges Take(long version, DiagramSnapshot snapshot, SchemaIndex index, DiagramTemplateSet? templates)
    {
        var changes = ScriptTables.Changes(version, snapshot, order, removed, index, templates);
        Clear();
        return changes;
    }
}
