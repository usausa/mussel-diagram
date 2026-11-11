namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Globalization;

internal static class DiagramValidator
{
    public static ImmutableArray<DiagramIssue> Validate(DiagramSnapshot snapshot, SchemaIndex index)
    {
        var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
        CheckLinks(snapshot, index, issues);
        CheckNesting(snapshot, index, issues);
        CheckCounts(snapshot, index, issues);
        CheckRequiredPorts(snapshot, index, issues);
        CheckSharedItems(snapshot, index, issues);
        CheckTypes(snapshot, index, issues);
        if (index.Schema.Rules.Check is { } check)
        {
            issues.AddRange(check(snapshot));
        }

        return issues.ToImmutable();
    }

    public static DiagramIssue Issue(DiagramIssueSeverity severity, DiagramIssueKind kind, ImmutableArray<string> elementIds, ImmutableArray<string> arguments, string? portId = null) =>
        new(severity, kind, BuiltInTexts.Issue(kind, arguments)) { ElementIds = elementIds, PortId = portId, Arguments = arguments };

    private static void CheckLinks(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        var positions = new Dictionary<string, int>(snapshot.Links.Length, StringComparer.Ordinal);
        for (var i = 0; i < snapshot.Links.Length; i++)
        {
            positions[snapshot.Links[i].Id] = i;
        }

        for (var i = 0; i < snapshot.Links.Length; i++)
        {
            var link = snapshot.Links[i];
            if ((MissingPort(link.Source) ?? MissingPort(link.Target)) is { } missing)
            {
                issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.MissingPort, [link.Id, missing.ElementId], [missing.PortId!], missing.PortId));
                continue;
            }

            var position = i;
            var reason = DiagramRules.CheckConnection(snapshot, index, link.Source, link.Target, link.Type, x => positions[x.Id] < position, link.Id, false);
            if (reason is not null)
            {
                issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.ConnectionRule, [link.Id], [reason.Message ?? string.Empty]));
            }
        }

        DiagramLinkEnd? MissingPort(DiagramLinkEnd end) =>
            (end.PortId is not null) && (snapshot.Find(end.ElementId) is DiagramBox box) && (index.FindBoxType(box) is not null) && (index.FindPort(box, end.PortId) is null) ? end : null;
    }

    private static void CheckNesting(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        foreach (var box in snapshot.Groups.Cast<DiagramBox>().Concat(snapshot.Nodes))
        {
            if (box is DiagramGroup { Kind: DiagramGroupKind.Overlay })
            {
                continue;
            }

            var reason = DiagramRules.CheckNesting(snapshot, index, box.Id, box.Type, box.ParentId);
            if (reason is not null)
            {
                issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.NestingRule, [box.Id], [reason.Message ?? string.Empty]));
            }
        }
    }

    private static void CheckCounts(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        var counts = new Dictionary<DiagramType, List<string>>(ReferenceEqualityComparer.Instance);
        foreach (var box in snapshot.Groups.Cast<DiagramBox>().Concat(snapshot.Nodes))
        {
            if (index.FindBoxType(box) is { } type)
            {
                Count(type, box.Id);
            }
        }

        foreach (var item in snapshot.SharedItems)
        {
            if (index.FindSharedItemType(item.Type) is { } type)
            {
                Count(type, item.Id);
            }
        }

        foreach (var type in index.BoxTypes)
        {
            Check(type, type.MaxCount, type.Required);
        }

        foreach (var type in index.SharedItemTypes)
        {
            Check(type, type.MaxCount, type.Required);
        }

        void Count(DiagramType type, string id)
        {
            if (!counts.TryGetValue(type, out var list))
            {
                list = [];
                counts[type] = list;
            }

            list.Add(id);
        }

        void Check(DiagramType type, int? maxCount, bool required)
        {
            var ids = counts.GetValueOrDefault(type);
            if ((maxCount is { } max) && (ids is not null) && (ids.Count > max))
            {
                issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.MaxCount, [.. ids], [type.Name, max.ToString(CultureInfo.InvariantCulture)]));
            }

            if (required && (ids is null))
            {
                issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.RequiredType, [], [type.Name]));
            }
        }
    }

    private static void CheckRequiredPorts(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        foreach (var box in snapshot.Groups.Cast<DiagramBox>().Concat(snapshot.Nodes))
        {
            if (index.FindBoxType(box) is null)
            {
                continue;
            }

            foreach (var port in index.PortsOf(box))
            {
                if (port.Definition.Required && snapshot.GetLinks(box.Id, DiagramLinkDirection.Both, port.Id).IsEmpty)
                {
                    issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.RequiredPort, [box.Id], [port.Id], port.Id));
                }
            }
        }
    }

    private static void CheckSharedItems(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        foreach (var element in snapshot.Groups.Cast<DiagramElement>().Concat(snapshot.Nodes).Concat(snapshot.Links).Concat(snapshot.SharedItems))
        {
            foreach (var id in DiagramRules.References(element, index).Distinct(StringComparer.Ordinal))
            {
                if (snapshot.Find(id) is not DiagramSharedItem)
                {
                    issues.Add(Issue(DiagramIssueSeverity.Error, DiagramIssueKind.MissingSharedItem, [element.Id], [id]));
                }
            }
        }
    }

    private static void CheckTypes(DiagramSnapshot snapshot, SchemaIndex index, ImmutableArray<DiagramIssue>.Builder issues)
    {
        foreach (var element in snapshot.Groups.Cast<DiagramElement>().Concat(snapshot.Nodes).Concat(snapshot.Links).Concat(snapshot.SharedItems))
        {
            if (index.FindType(element) is null)
            {
                issues.Add(Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.UnregisteredType, [element.Id], [element.Type]));
            }
        }
    }
}
