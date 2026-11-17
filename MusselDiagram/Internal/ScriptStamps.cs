namespace MusselDiagram.Internal;

internal sealed class ScriptStamps
{
    private readonly Dictionary<string, Stamp> stamps = new(StringComparer.Ordinal);

    private long floor;

    public void Reset(long version)
    {
        stamps.Clear();
        floor = version;
    }

    public void Record(DiagramChangedEventArgs change, long version)
    {
        if (change.IsReset)
        {
            foreach (var element in Elements(change.After))
            {
                Compare(change.Before.Find(element.Id), element, version);
            }

            foreach (var element in Elements(change.Before))
            {
                if (!change.After.Contains(element.Id))
                {
                    Compare(element, null, version);
                }
            }

            return;
        }

        foreach (var id in change.Deleted)
        {
            Compare(change.Before.Find(id), null, version);
        }

        foreach (var id in change.Added)
        {
            Compare(null, change.After.Find(id), version);
        }

        foreach (var id in change.Updated)
        {
            Compare(change.Before.Find(id), change.After.Find(id), version);
        }
    }

    public bool BoxChanged(string id, long since) =>
        (since < floor) || (stamps.TryGetValue(id, out var stamp) && (Math.Max(Math.Max(stamp.Created, stamp.Bounds), Math.Max(Math.Max(stamp.Parent, stamp.Children), stamp.Members)) > since));

    public bool BoundsChanged(string id, long since) =>
        (since < floor) || (stamps.TryGetValue(id, out var stamp) && (Math.Max(stamp.Created, stamp.Bounds) > since));

    public bool EndsChanged(string id, long since) =>
        (since < floor) || (stamps.TryGetValue(id, out var stamp) && (Math.Max(stamp.Created, stamp.Ends) > since));

    public bool TextChanged(string id, string? slot, long since) =>
        (since < floor) || (stamps.TryGetValue(id, out var stamp) && (Math.Max(stamp.Created, slot is null ? stamp.Label : stamp.Texts?.GetValueOrDefault(slot) ?? 0) > since));

    private static IEnumerable<DiagramElement> Elements(DiagramSnapshot snapshot)
    {
        foreach (var group in snapshot.Groups)
        {
            yield return group;
        }

        foreach (var node in snapshot.Nodes)
        {
            yield return node;
        }

        foreach (var link in snapshot.Links)
        {
            yield return link;
        }
    }

    private void Compare(DiagramElement? before, DiagramElement? after, long version)
    {
        if (ReferenceEquals(before, after))
        {
            return;
        }

        switch (before, after)
        {
            case (null, { } added):
                StampOf(added.Id).Created = version;
                Touch((added as DiagramBox)?.ParentId, version);
                break;
            case ({ } removed, null):
                stamps.Remove(removed.Id);
                Touch((removed as DiagramBox)?.ParentId, version);
                break;
            case (DiagramBox old, DiagramBox now):
                CompareBoxes(old, now, version);
                break;
            case (DiagramLink old, DiagramLink now):
                if ((old.Source != now.Source) || (old.Target != now.Target))
                {
                    StampOf(now.Id).Ends = version;
                }

                break;
            case (_, { } other):
                StampOf(other.Id).Created = version;
                break;
        }
    }

    private void CompareBoxes(DiagramBox old, DiagramBox now, long version)
    {
        if (old.Bounds != now.Bounds)
        {
            StampOf(now.Id).Bounds = version;
        }

        if (!string.Equals(old.ParentId, now.ParentId, StringComparison.Ordinal))
        {
            StampOf(now.Id).Parent = version;
            Touch(old.ParentId, version);
            Touch(now.ParentId, version);
        }

        if (!string.Equals(old.Label, now.Label, StringComparison.Ordinal))
        {
            StampOf(now.Id).Label = version;
        }

        if (!ReferenceEquals(old.Texts, now.Texts))
        {
            foreach (var (slot, text) in now.Texts)
            {
                if (!string.Equals(old.Texts.GetValueOrDefault(slot), text, StringComparison.Ordinal))
                {
                    MarkText(now.Id, slot, version);
                }
            }

            foreach (var slot in old.Texts.Keys)
            {
                if (!now.Texts.ContainsKey(slot))
                {
                    MarkText(now.Id, slot, version);
                }
            }
        }

        if ((old is DiagramGroup oldGroup) && (now is DiagramGroup newGroup) && ((oldGroup.Kind != newGroup.Kind) || !oldGroup.MemberIds.SequenceEqual(newGroup.MemberIds, StringComparer.Ordinal)))
        {
            StampOf(now.Id).Members = version;
        }
    }

    private void MarkText(string id, string slot, long version)
    {
        var stamp = StampOf(id);
        stamp.Texts ??= new Dictionary<string, long>(StringComparer.Ordinal);
        stamp.Texts[slot] = version;
    }

    private void Touch(string? parentId, long version)
    {
        if (parentId is not null)
        {
            StampOf(parentId).Children = version;
        }
    }

    private Stamp StampOf(string id)
    {
        if (!stamps.TryGetValue(id, out var stamp))
        {
            stamp = new Stamp();
            stamps[id] = stamp;
        }

        return stamp;
    }

    private sealed class Stamp
    {
        public long Created { get; set; }

        public long Bounds { get; set; }

        public long Parent { get; set; }

        public long Children { get; set; }

        public long Members { get; set; }

        public long Ends { get; set; }

        public long Label { get; set; }

        public Dictionary<string, long>? Texts { get; set; }
    }
}
