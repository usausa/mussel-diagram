namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record GroupingResult(Action<SnapshotEditor>? Edit, ImmutableArray<string> Selection, DiagramReason? Reason)
{
    public static GroupingResult None { get; } = new(null, [], null);

    public static GroupingResult Rejected(DiagramReasonKind kind) => new(null, [], new DiagramReason(kind, BuiltInTexts.Reason(kind)));

    public static GroupingResult Rejected(DiagramReason reason) => new(null, [], reason);
}

internal static class Grouping
{
    public static bool CanGroup(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, string? typeName) =>
        GroupTarget(snapshot, index, selection, typeName) is not null;

    public static GroupingResult Group(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, string? typeName)
    {
        if (GroupTarget(snapshot, index, selection, typeName) is not var (type, boxes, parentId))
        {
            return GroupingResult.None;
        }

        var left = boxes.Min(static x => x.Bounds.X) - type.Padding.Left;
        var top = boxes.Min(static x => x.Bounds.Y) - type.Padding.Top;
        var right = boxes.Max(static x => x.Bounds.Right) + type.Padding.Right;
        var bottom = boxes.Max(static x => x.Bounds.Bottom) + type.Padding.Bottom;
        var group = new DiagramGroup { Type = type.Name, Bounds = new DiagramRect(left, top, right - left, bottom - top), ParentId = parentId, Data = type.DefaultData };
        var children = boxes.Select(static x => x.Id).ToArray();
        var editor = new SnapshotEditor(snapshot, index) { SkipRoutes = true };
        editor.Add(group);
        editor.Complete();
        var trial = editor.Build(snapshot.Version);
        if (DiagramRules.CheckNesting(trial, index, group.Id, group.Type, parentId) is { } reason)
        {
            return GroupingResult.Rejected(reason);
        }

        foreach (var box in boxes)
        {
            if (DiagramRules.CheckNesting(trial, index, box.Id, box.Type, group.Id) is { } childReason)
            {
                return GroupingResult.Rejected(childReason);
            }
        }

        return new GroupingResult(
            target =>
            {
                target.Add(group);
                target.SetParent(children, group.Id);
            },
            [group.Id],
            null);
    }

    public static bool CanUngroup(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection) =>
        UngroupTargets(snapshot, index, selection).Count > 0;

    public static GroupingResult Ungroup(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection)
    {
        var groups = UngroupTargets(snapshot, index, selection);
        if (groups.Count == 0)
        {
            return GroupingResult.None;
        }

        var removed = groups.Where(static x => x.Kind == DiagramGroupKind.Tree).Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        var moves = new List<(string[] Children, string? ParentId)>();
        var after = new List<string>();
        foreach (var group in groups)
        {
            if (group.Kind == DiagramGroupKind.Overlay)
            {
                after.AddRange(group.MemberIds);
                continue;
            }

            var parentId = group.ParentId;
            while ((parentId is not null) && removed.Contains(parentId))
            {
                parentId = snapshot.Find(parentId) is DiagramBox parent ? parent.ParentId : null;
            }

            var children = snapshot.GetChildren(group.Id).Where(x => !removed.Contains(x.Id)).ToArray();
            foreach (var child in children)
            {
                if (!ElementPermissions.Allows(child, index, DiagramPermissions.Move))
                {
                    return GroupingResult.Rejected(DiagramReasonKind.Permission);
                }

                if (DiagramRules.CheckNesting(snapshot, index, child.Id, child.Type, parentId) is { } reason)
                {
                    return GroupingResult.Rejected(reason);
                }
            }

            moves.Add(([.. children.Select(static x => x.Id)], parentId));
            after.AddRange(children.Select(static x => x.Id));
        }

        var deleted = groups.Select(static x => x.Id).ToArray();
        return new GroupingResult(
            editor =>
            {
                foreach (var (children, parentId) in moves)
                {
                    editor.SetParent(children, parentId);
                }

                editor.Delete(deleted);
            },
            [.. after.Distinct(StringComparer.Ordinal).Where(x => !removed.Contains(x))],
            null);
    }

    public static bool CanCreateOverlay(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, string? typeName) =>
        (OverlayType(index, typeName) is not null) && (Boxes(snapshot, selection).Count > 0);

    public static GroupingResult CreateOverlay(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, string? typeName)
    {
        var members = Boxes(snapshot, selection);
        if ((OverlayType(index, typeName) is not { } type) || (members.Count == 0))
        {
            return GroupingResult.None;
        }

        if (CheckMembers(type, members) is { } reason)
        {
            return GroupingResult.Rejected(reason);
        }

        var group = new DiagramGroup { Type = type.Name, Kind = DiagramGroupKind.Overlay, MemberIds = [.. members.Select(static x => x.Id)], Data = type.DefaultData };
        return new GroupingResult(editor => editor.Add(group), [group.Id], null);
    }

    public static bool CanAddMembers(DiagramSnapshot snapshot, IEnumerable<string> selection) =>
        (MemberTarget(snapshot, selection) is var (group, others)) && others.Any(x => !group.MemberIds.Contains(x.Id));

    public static GroupingResult AddMembers(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection)
    {
        if (MemberTarget(snapshot, selection) is not var (group, others))
        {
            return GroupingResult.None;
        }

        var added = others.Where(x => !group.MemberIds.Contains(x.Id)).ToList();
        if (added.Count == 0)
        {
            return GroupingResult.None;
        }

        if ((index.FindGroupType(group.Type) is { } type) && (CheckMembers(type, added) is { } reason))
        {
            return GroupingResult.Rejected(reason);
        }

        string[] members = [.. group.MemberIds, .. added.Select(static x => x.Id)];
        return new GroupingResult(editor => editor.SetMembers(group.Id, members), [group.Id], null);
    }

    public static bool CanRemoveMembers(DiagramSnapshot snapshot, IEnumerable<string> selection) =>
        (MemberTarget(snapshot, selection) is var (group, others)) && others.Any(x => group.MemberIds.Contains(x.Id));

    public static GroupingResult RemoveMembers(DiagramSnapshot snapshot, IEnumerable<string> selection)
    {
        if (MemberTarget(snapshot, selection) is not var (group, others))
        {
            return GroupingResult.None;
        }

        var removed = others.Where(x => group.MemberIds.Contains(x.Id)).Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (removed.Count == 0)
        {
            return GroupingResult.None;
        }

        string[] members = [.. group.MemberIds.Where(x => !removed.Contains(x))];
        return new GroupingResult(editor => editor.SetMembers(group.Id, members), members.Length > 0 ? [group.Id] : [], null);
    }

    private static (DiagramGroupType Type, List<DiagramBox> Boxes, string? ParentId)? GroupTarget(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, string? typeName)
    {
        var name = typeName ?? index.Schema.DefaultGroupType;
        if ((name is null) || (index.FindGroupType(name) is not { Kind: DiagramGroupKind.Tree } type))
        {
            return null;
        }

        var boxes = Boxes(snapshot, selection);
        if (boxes.Count == 0)
        {
            return null;
        }

        var parentId = boxes[0].ParentId;
        if (boxes.Any(x => !string.Equals(x.ParentId, parentId, StringComparison.Ordinal) || !ElementPermissions.Allows(x, index, DiagramPermissions.Move)))
        {
            return null;
        }

        return (type, boxes, parentId);
    }

    private static List<DiagramGroup> UngroupTargets(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection) =>
        [.. selection.Distinct(StringComparer.Ordinal).Select(snapshot.Find).OfType<DiagramGroup>()
            .Where(x => ElementPermissions.Allows(x, index, DiagramPermissions.Delete) && (index.FindBoxType(x) is not { Undeletable: true }))];

    private static DiagramGroupType? OverlayType(SchemaIndex index, string? typeName) =>
        (typeName is not null) && (index.FindGroupType(typeName) is { Kind: DiagramGroupKind.Overlay } type) ? type : null;

    private static (DiagramGroup Group, List<DiagramBox> Others)? MemberTarget(DiagramSnapshot snapshot, IEnumerable<string> selection)
    {
        var selected = selection.Distinct(StringComparer.Ordinal).Select(snapshot.Find).OfType<DiagramBox>().ToList();
        var overlays = selected.OfType<DiagramGroup>().Where(static x => x.Kind == DiagramGroupKind.Overlay).ToList();
        if ((overlays.Count != 1) || overlays[0].Locked)
        {
            return null;
        }

        return (overlays[0], [.. selected.Where(static x => x is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree })]);
    }

    private static List<DiagramBox> Boxes(DiagramSnapshot snapshot, IEnumerable<string> selection) =>
        [.. selection.Distinct(StringComparer.Ordinal).Select(snapshot.Find).OfType<DiagramBox>().Where(static x => x is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree })];

    private static DiagramReason? CheckMembers(DiagramGroupType type, IEnumerable<DiagramBox> members) =>
        (type.AllowedMembers is { } allowed) && members.Any(x => !allowed.Contains(x.Type))
            ? new DiagramReason(DiagramReasonKind.Nesting, BuiltInTexts.Reason(DiagramReasonKind.Nesting))
            : null;
}
