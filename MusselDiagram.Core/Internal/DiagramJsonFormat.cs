namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal static class DiagramJsonFormat
{
    public const string FormatName = "mussel-diagram";

    public const int CurrentVersion = 1;

    private static readonly (DiagramPermissions Flag, string Name)[] PermissionNames =
    [
        (DiagramPermissions.Select, "select"),
        (DiagramPermissions.Move, "move"),
        (DiagramPermissions.Resize, "resize"),
        (DiagramPermissions.Delete, "delete"),
        (DiagramPermissions.Connect, "connect"),
        (DiagramPermissions.EditText, "editText")
    ];

    private static readonly DiagramSize DefaultSize = new DiagramNodeType { Name = string.Empty }.Size;

    private static JsonTypeInfo<DiagramJsonDocument> DocumentInfo => DiagramJsonContext.Default.DiagramJsonDocument;

    public static void Write(Stream stream, DiagramSnapshot snapshot, SchemaIndex index) =>
        JsonSerializer.Serialize(stream, ToDocument(snapshot, index), DocumentInfo);

    public static Task WriteAsync(Stream stream, DiagramSnapshot snapshot, SchemaIndex index, CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(stream, ToDocument(snapshot, index), DocumentInfo, cancellationToken);

    public static string ToJson(DiagramSnapshot snapshot, SchemaIndex index) =>
        JsonSerializer.Serialize(ToDocument(snapshot, index), DocumentInfo);

    public static DiagramJsonDocument Parse(Stream stream)
    {
        try
        {
            return Check(JsonSerializer.Deserialize(stream, DocumentInfo));
        }
        catch (JsonException ex)
        {
            throw new DiagramException("The text is not the JSON of a diagram.", ex);
        }
    }

    public static async Task<DiagramJsonDocument> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return Check(await JsonSerializer.DeserializeAsync(stream, DocumentInfo, cancellationToken).ConfigureAwait(false));
        }
        catch (JsonException ex)
        {
            throw new DiagramException("The text is not the JSON of a diagram.", ex);
        }
    }

    public static DiagramJsonDocument Parse(string json)
    {
        try
        {
            return Check(JsonSerializer.Deserialize(json, DocumentInfo));
        }
        catch (JsonException ex)
        {
            throw new DiagramException("The text is not the JSON of a diagram.", ex);
        }
    }

    public static DiagramLoadResult Read(DiagramJsonDocument document, SchemaIndex index, long version, RouteBudget? budget = null) =>
        new Reader(index).Read(document, version, budget);

    private static DiagramJsonDocument Check(DiagramJsonDocument? document)
    {
        if ((document is null) || !string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new DiagramException("The JSON is not a diagram.");
        }

        if (document.Version > CurrentVersion)
        {
            throw new DiagramException($"The version {document.Version} of the diagram is newer than {CurrentVersion}.");
        }

        if (document.Version < 1)
        {
            throw new DiagramException($"The version {document.Version} of the diagram is not supported.");
        }

        return document;
    }

    private static DiagramJsonDocument ToDocument(DiagramSnapshot snapshot, SchemaIndex index) => new()
    {
        Format = FormatName,
        Version = CurrentVersion,
        Groups = [.. snapshot.Groups.Select(x => ToJson(x, index))],
        Nodes = [.. snapshot.Nodes.Select(x => ToJson(x, index))],
        Links = [.. snapshot.Links.Select(x => ToJson(x, index))],
        SharedItems = [.. snapshot.SharedItems.Select(x => ToJson(x, index))]
    };

    private static DiagramJsonBox ToJson(DiagramBox box, SchemaIndex index)
    {
        var overlay = box is DiagramGroup { Kind: DiagramGroupKind.Overlay };
        return new DiagramJsonBox
        {
            Id = box.Id,
            Type = box.Type,
            Kind = box is DiagramGroup ? (overlay ? "overlay" : "tree") : null,
            X = overlay ? null : box.Bounds.X,
            Y = overlay ? null : box.Bounds.Y,
            Width = overlay ? null : box.Bounds.Width,
            Height = overlay ? null : box.Bounds.Height,
            Parent = overlay ? null : box.ParentId,
            Label = string.IsNullOrEmpty(box.Label) ? null : box.Label,
            Texts = box.Texts.IsEmpty ? null : box.Texts.OrderBy(static x => x.Key, StringComparer.Ordinal).ToDictionary(static x => x.Key, static x => (string?)x.Value, StringComparer.Ordinal),
            Icon = box.Icon,
            Data = WriteData(box, index),
            Locked = box.Locked,
            Permissions = WritePermissions(box.Permissions),
            Members = box is DiagramGroup { MemberIds.IsEmpty: false } group ? [.. group.MemberIds] : null
        };
    }

    private static DiagramJsonLink ToJson(DiagramLink link, SchemaIndex index) => new()
    {
        Id = link.Id,
        Type = link.Type,
        Source = new DiagramJsonEnd { Element = link.Source.ElementId, Port = link.Source.PortId },
        Target = new DiagramJsonEnd { Element = link.Target.ElementId, Port = link.Target.PortId },
        Route = (link.RouteState == DiagramRouteState.Routed) && !link.Route.IsEmpty ? [.. link.Route.Select(static x => (IReadOnlyList<double>)[x.X, x.Y])] : null,
        Data = WriteData(link, index),
        Locked = link.Locked,
        Permissions = WritePermissions(link.Permissions)
    };

    private static DiagramJsonSharedItem ToJson(DiagramSharedItem item, SchemaIndex index) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Label = string.IsNullOrEmpty(item.Label) ? null : item.Label,
        Data = WriteData(item, index)
    };

    private static JsonElement? WriteData(DiagramElement element, SchemaIndex index) => element.Data switch
    {
        null => null,
        JsonElement json => json,
        var data => JsonSerializer.SerializeToElement(data, DataTypeOf(element, data, index))
    };

    private static JsonTypeInfo DataTypeOf(DiagramElement element, object data, SchemaIndex index) =>
        (index.FindType(element)?.DataType is { } dataType) && dataType.Type.IsInstanceOfType(data)
            ? dataType
            : SchemaIndex.FindDataType(data.GetType()) ?? throw new DiagramException($"The data of '{element.Id}' has no registered data type.");

    private static List<string>? WritePermissions(DiagramPermissions? permissions)
    {
        if (permissions is not { } value)
        {
            return null;
        }

        var names = new List<string>();
        foreach (var (flag, name) in PermissionNames)
        {
            if ((value & flag) == flag)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private sealed class Reader(SchemaIndex index)
    {
        private readonly ImmutableArray<DiagramIssue>.Builder issues = ImmutableArray.CreateBuilder<DiagramIssue>();

        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        public DiagramLoadResult Read(DiagramJsonDocument document, long version, RouteBudget? budget)
        {
            var boxes = new List<DiagramBox>();
            var links = new List<DiagramLink>();
            var items = new List<DiagramSharedItem>();
            foreach (var json in document.Groups ?? [])
            {
                if (Claim(json.Id) is { } id)
                {
                    boxes.Add(ReadBox(json, id, true));
                }
            }

            foreach (var json in document.Nodes ?? [])
            {
                if (Claim(json.Id) is { } id)
                {
                    boxes.Add(ReadBox(json, id, false));
                }
            }

            foreach (var json in document.Links ?? [])
            {
                if ((Claim(json.Id) is { } id) && (ReadLink(json, id) is { } link))
                {
                    links.Add(link);
                }
            }

            foreach (var json in document.SharedItems ?? [])
            {
                if (Claim(json.Id) is { } id)
                {
                    items.Add(ReadSharedItem(json, id));
                }
            }

            var byId = boxes.ToDictionary(static x => x.Id, StringComparer.Ordinal);
            FixParents(boxes, byId);
            FixMembers(boxes, byId);

            var editor = new SnapshotEditor(DiagramSnapshot.Empty, index) { SkipKindCheck = true, SkipCycleCheck = true, Budget = budget };
            foreach (var group in ParentsFirst(boxes, byId))
            {
                editor.Add(group);
            }

            foreach (var box in boxes)
            {
                if (box is DiagramNode)
                {
                    editor.Add(box);
                }
            }

            foreach (var box in boxes)
            {
                if (box is DiagramGroup { Kind: DiagramGroupKind.Overlay })
                {
                    editor.Add(box);
                }
            }

            editor.Complete();
            foreach (var link in links)
            {
                if (byId.ContainsKey(link.Source.ElementId) && byId.ContainsKey(link.Target.ElementId))
                {
                    editor.AddWithRoute(link);
                }
                else
                {
                    Report(DiagramIssueSeverity.Error, DiagramIssueKind.MissingEnd, link.Id);
                }
            }

            foreach (var item in items)
            {
                editor.Add(item);
            }

            editor.Complete();
            return new DiagramLoadResult(editor.Build(version), issues.ToImmutable()) { PendingRoutes = editor.PendingRoutes };
        }

        private static IEnumerable<DiagramGroup> ParentsFirst(List<DiagramBox> boxes, Dictionary<string, DiagramBox> byId)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);
            var chain = new List<DiagramGroup>();
            foreach (var box in boxes)
            {
                if (box is not DiagramGroup { Kind: DiagramGroupKind.Tree } group)
                {
                    continue;
                }

                chain.Clear();
                var current = group;
                while ((current is not null) && !placed.Contains(current.Id))
                {
                    chain.Add(current);
                    current = current.ParentId is { } parentId ? byId[parentId] as DiagramGroup : null;
                }

                for (var i = chain.Count - 1; i >= 0; i--)
                {
                    placed.Add(chain[i].Id);
                    yield return chain[i];
                }
            }
        }

        private static DiagramPermissions? ReadPermissions(IReadOnlyList<string>? names)
        {
            if (names is null)
            {
                return null;
            }

            var value = DiagramPermissions.None;
            foreach (var name in names)
            {
                foreach (var (flag, text) in PermissionNames)
                {
                    if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                    {
                        value |= flag;
                    }
                }
            }

            return value;
        }

        private static ImmutableArray<DiagramPoint> ReadRoute(IReadOnlyList<IReadOnlyList<double>>? route)
        {
            if (route is null)
            {
                return [];
            }

            var builder = ImmutableArray.CreateBuilder<DiagramPoint>(route.Count);
            foreach (var point in route)
            {
                if ((point is not { Count: 2 }) || !double.IsFinite(point[0]) || !double.IsFinite(point[1]))
                {
                    return [];
                }

                builder.Add(new DiagramPoint(point[0], point[1]));
            }

            return builder.MoveToImmutable();
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

        private static double Finite(double? value) => (value is { } number) && double.IsFinite(number) ? number : 0;

        private string? Claim(string? id)
        {
            var value = string.IsNullOrEmpty(id) ? DiagramIds.Create() : id;
            if (seen.Add(value))
            {
                return value;
            }

            Report(DiagramIssueSeverity.Error, DiagramIssueKind.DuplicateId, value, value);
            return null;
        }

        private DiagramBox ReadBox(DiagramJsonBox json, string id, bool isGroup)
        {
            var type = json.Type ?? string.Empty;
            var kind = isGroup && string.Equals(json.Kind, "overlay", StringComparison.OrdinalIgnoreCase) ? DiagramGroupKind.Overlay : DiagramGroupKind.Tree;
            DiagramBox box = isGroup
                ? new DiagramGroup { Id = id, Type = type, Kind = kind, MemberIds = kind == DiagramGroupKind.Overlay ? [.. (json.Members ?? []).Where(static x => !string.IsNullOrEmpty(x))] : [] }
                : new DiagramNode { Id = id, Type = type };
            if (isGroup && (index.FindGroupType(type) is { } groupType) && (groupType.Kind != kind))
            {
                Report(DiagramIssueSeverity.Warning, DiagramIssueKind.KindMismatch, id, type);
            }

            var boxType = index.FindBoxType(box);
            var bounds = default(DiagramRect);
            if (kind != DiagramGroupKind.Overlay)
            {
                var size = boxType?.Size ?? DefaultSize;
                var width = json.Width ?? size.Width;
                var height = json.Height ?? size.Height;
                if (!double.IsFinite(width) || !double.IsFinite(height) || (width <= 0) || (height <= 0))
                {
                    Report(DiagramIssueSeverity.Error, DiagramIssueKind.InvalidSize, id);
                    width = size.Width;
                    height = size.Height;
                }

                bounds = new DiagramRect(Finite(json.X), Finite(json.Y), width, height);
            }

            var texts = json.Texts?.Where(static x => x.Value is not null).ToImmutableDictionary(static x => x.Key, static x => x.Value!, StringComparer.Ordinal);
            return box with
            {
                Bounds = bounds,
                ParentId = kind == DiagramGroupKind.Overlay ? null : NullIfEmpty(json.Parent),
                Label = json.Label,
                Texts = texts is { IsEmpty: false } ? texts : box.Texts,
                Icon = NullIfEmpty(json.Icon),
                Data = ReadData(json.Data, id, type, boxType),
                Locked = json.Locked,
                Permissions = ReadPermissions(json.Permissions)
            };
        }

        private DiagramLink? ReadLink(DiagramJsonLink json, string id)
        {
            if ((json.Source?.Element is not { Length: > 0 } source) || (json.Target?.Element is not { Length: > 0 } target))
            {
                Report(DiagramIssueSeverity.Error, DiagramIssueKind.MissingEnd, id);
                return null;
            }

            var type = json.Type ?? string.Empty;
            return new DiagramLink
            {
                Id = id,
                Type = type,
                Source = new DiagramLinkEnd(source, NullIfEmpty(json.Source.Port)),
                Target = new DiagramLinkEnd(target, NullIfEmpty(json.Target.Port)),
                Route = ReadRoute(json.Route),
                Data = ReadData(json.Data, id, type, index.FindLinkType(type)),
                Locked = json.Locked,
                Permissions = ReadPermissions(json.Permissions)
            };
        }

        private DiagramSharedItem ReadSharedItem(DiagramJsonSharedItem json, string id)
        {
            var type = json.Type ?? string.Empty;
            return new DiagramSharedItem
            {
                Id = id,
                Type = type,
                Label = json.Label,
                Data = ReadData(json.Data, id, type, index.FindSharedItemType(type))
            };
        }

        private object? ReadData(JsonElement? json, string id, string typeName, DiagramType? type)
        {
            if ((json is not { } value) || (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined))
            {
                return null;
            }

            if (type?.DataType is not { } dataType)
            {
                return value;
            }

            try
            {
                return value.Deserialize(dataType);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                Report(DiagramIssueSeverity.Warning, DiagramIssueKind.UnreadableData, id, typeName);
                return value;
            }
        }

        private void FixParents(List<DiagramBox> boxes, Dictionary<string, DiagramBox> byId)
        {
            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if ((box.ParentId is { } parentId) && (byId.GetValueOrDefault(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree }))
                {
                    Report(DiagramIssueSeverity.Error, DiagramIssueKind.InvalidParent, box.Id, parentId);
                    Set(i, box with { ParentId = null });
                }
            }

            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                var current = box.ParentId;
                for (var steps = 0; (current is not null) && (steps <= boxes.Count); steps++)
                {
                    if (string.Equals(current, box.Id, StringComparison.Ordinal))
                    {
                        Report(DiagramIssueSeverity.Error, DiagramIssueKind.InvalidParent, box.Id, box.ParentId!);
                        Set(i, box with { ParentId = null });
                        break;
                    }

                    current = byId[current].ParentId;
                }
            }

            void Set(int position, DiagramBox box)
            {
                boxes[position] = box;
                byId[box.Id] = box;
            }
        }

        private void FixMembers(List<DiagramBox> boxes, Dictionary<string, DiagramBox> byId)
        {
            var dropped = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i] is not DiagramGroup { Kind: DiagramGroupKind.Overlay } group)
                {
                    continue;
                }

                var members = new List<string>();
                foreach (var memberId in group.MemberIds)
                {
                    if (byId.GetValueOrDefault(memberId) is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree })
                    {
                        if (!members.Contains(memberId, StringComparer.Ordinal))
                        {
                            members.Add(memberId);
                        }
                    }
                    else
                    {
                        Report(DiagramIssueSeverity.Error, DiagramIssueKind.InvalidMember, group.Id, memberId);
                    }
                }

                if (members.Count == 0)
                {
                    if (group.MemberIds.IsEmpty)
                    {
                        Report(DiagramIssueSeverity.Error, DiagramIssueKind.NoMembers, group.Id);
                    }

                    dropped.Add(group.Id);
                    byId.Remove(group.Id);
                }
                else if (members.Count != group.MemberIds.Length)
                {
                    boxes[i] = group with { MemberIds = [.. members] };
                    byId[group.Id] = boxes[i];
                }
            }

            boxes.RemoveAll(x => dropped.Contains(x.Id));
        }

        private void Report(DiagramIssueSeverity severity, DiagramIssueKind kind, string elementId, params string[] arguments) =>
            issues.Add(DiagramValidator.Issue(severity, kind, [elementId], [.. arguments]));
    }
}
