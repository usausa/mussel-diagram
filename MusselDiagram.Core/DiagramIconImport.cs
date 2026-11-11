namespace MusselDiagram;

using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

using MusselDiagram.Internal;

public sealed record DiagramIconInfo(string Id, string? Name = null, string? Category = null);

public sealed record DiagramIconImportResult(DiagramIconSet IconSet, ImmutableArray<DiagramIssue> Issues);

public static class DiagramIconImport
{
    private const int MaxFileLength = 4 * 1024 * 1024;

    public static DiagramIconImportResult FromIconify(Stream stream)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stream);
        }
        catch (JsonException e)
        {
            throw new DiagramException("The stream is not an Iconify icon set.", e);
        }

        using (document)
        {
            var root = document.RootElement;
            if ((root.ValueKind != JsonValueKind.Object) ||
                !root.TryGetProperty("prefix", out var prefixValue) || (prefixValue.GetString() is not { Length: > 0 } prefix) ||
                !root.TryGetProperty("icons", out var iconValues) || (iconValues.ValueKind != JsonValueKind.Object))
            {
                throw new DiagramException("The stream is not an Iconify icon set.");
            }

            var width = Number(root, "width") ?? 16;
            var height = Number(root, "height") ?? 16;
            var categories = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("categories", out var categoryValues) && (categoryValues.ValueKind == JsonValueKind.Object))
            {
                foreach (var category in categoryValues.EnumerateObject())
                {
                    if (category.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var name in category.Value.EnumerateArray())
                        {
                            if (name.GetString() is { } id)
                            {
                                categories.TryAdd(id, category.Name);
                            }
                        }
                    }
                }
            }

            var icons = ImmutableArray.CreateBuilder<DiagramIcon>();
            var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
            foreach (var icon in iconValues.EnumerateObject())
            {
                Add(icon.Name, icon.Value, default);
            }

            if (root.TryGetProperty("aliases", out var aliases) && (aliases.ValueKind == JsonValueKind.Object))
            {
                foreach (var alias in aliases.EnumerateObject())
                {
                    if (alias.Value.TryGetProperty("parent", out var parent) && (parent.GetString() is { } parentId) && iconValues.TryGetProperty(parentId, out var parentValue))
                    {
                        Add(alias.Name, parentValue, alias.Value);
                    }
                }
            }

            return new DiagramIconImportResult(new DiagramIconSet { Prefix = prefix, Icons = icons.ToImmutable() }, issues.ToImmutable());

            void Add(string id, JsonElement value, JsonElement alias)
            {
                if (!value.TryGetProperty("body", out var bodyValue) || (bodyValue.GetString() is not { } body))
                {
                    return;
                }

                var hasAlias = alias.ValueKind == JsonValueKind.Object;
                var iconWidth = (hasAlias ? Number(alias, "width") : null) ?? Number(value, "width") ?? width;
                var iconHeight = (hasAlias ? Number(alias, "height") : null) ?? Number(value, "height") ?? height;
                var left = (hasAlias ? Number(alias, "left") : null) ?? Number(value, "left") ?? 0;
                var top = (hasAlias ? Number(alias, "top") : null) ?? Number(value, "top") ?? 0;
                var rotate = (int)((Number(value, "rotate") ?? 0) + (hasAlias ? Number(alias, "rotate") ?? 0 : 0)) % 4;
                var horizontalFlip = Flag(value, "hFlip") ^ (hasAlias && Flag(alias, "hFlip"));
                var verticalFlip = Flag(value, "vFlip") ^ (hasAlias && Flag(alias, "vFlip"));
                var transforms = new List<string>();
                if (rotate != 0)
                {
                    transforms.Add($"rotate({rotate * 90} {SvgSanitizer.Number(iconWidth / 2)} {SvgSanitizer.Number(iconHeight / 2)})");
                }

                if (horizontalFlip)
                {
                    transforms.Add($"translate({SvgSanitizer.Number(iconWidth)} 0) scale(-1 1)");
                }

                if (verticalFlip)
                {
                    transforms.Add($"translate(0 {SvgSanitizer.Number(iconHeight)}) scale(1 -1)");
                }

                if ((left != 0) || (top != 0))
                {
                    transforms.Add($"translate({SvgSanitizer.Number(-left)} {SvgSanitizer.Number(-top)})");
                }

                if (transforms.Count > 0)
                {
                    body = $"<g transform=\"{string.Join(' ', transforms)}\">{body}</g>";
                }

                if (SvgSanitizer.FromBody(body, IdPrefix(prefix, id), iconWidth, iconHeight) is not { } svg)
                {
                    issues.Add(Unreadable(id));
                    return;
                }

                Report(id, svg, issues);
                icons.Add(new DiagramIcon { Id = id, Body = svg.Body, Width = svg.Width, Height = svg.Height, Name = id, Category = categories.GetValueOrDefault(id) });
            }
        }
    }

    public static DiagramIconImportResult FromZip(Stream stream, string prefix, Func<string, DiagramIconInfo?>? map = null)
    {
        var files = new List<(string Path, string? Text)>();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    using var content = entry.Open();
                    files.Add((entry.FullName.Replace('\\', '/'), Read(content)));
                }
            }
        }

        return FromFiles(files, prefix, map);
    }

    public static DiagramIconImportResult FromFolder(string path, string prefix, Func<string, DiagramIconInfo?>? map = null)
    {
        var files = new List<(string Path, string? Text)>();
        foreach (var file in Directory.EnumerateFiles(path, "*.svg", SearchOption.AllDirectories))
        {
            var text = new FileInfo(file).Length > MaxFileLength ? null : File.ReadAllText(file, Encoding.UTF8);
            files.Add((Path.GetRelativePath(path, file).Replace('\\', '/'), text));
        }

        return FromFiles(files, prefix, map);
    }

    public static ImmutableArray<DiagramNodeType> CreateNodeTypes(DiagramIconSet set, DiagramNodeType baseType, Func<DiagramIcon, bool>? filter = null) =>
        [.. set.Icons.Where(x => filter?.Invoke(x) ?? true).Select(icon => baseType with
        {
            Name = $"{set.Prefix}:{icon.Id}",
            DisplayName = icon.Name ?? icon.Id,
            Category = icon.Category ?? baseType.Category,
            SearchWords = icon.SearchWords,
            Look = baseType.Look with { Icon = $"{set.Prefix}:{icon.Id}" }
        })];

    public static ImmutableArray<DiagramGroupType> CreateGroupTypes(DiagramIconSet set, DiagramGroupType baseType, Func<DiagramIcon, bool>? filter = null) =>
        [.. set.Icons.Where(x => filter?.Invoke(x) ?? true).Select(icon => baseType with
        {
            Name = $"{set.Prefix}:{icon.Id}",
            DisplayName = icon.Name ?? icon.Id,
            Category = icon.Category ?? baseType.Category,
            SearchWords = icon.SearchWords,
            Look = baseType.Look with { Icon = $"{set.Prefix}:{icon.Id}" }
        })];

    private static DiagramIconImportResult FromFiles(IEnumerable<(string Path, string? Text)> files, string prefix, Func<string, DiagramIconInfo?>? map)
    {
        map ??= DefaultMap;
        var icons = ImmutableArray.CreateBuilder<DiagramIcon>();
        var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, text) in files.OrderBy(static x => x.Path, StringComparer.Ordinal))
        {
            if ((map(path) is not { } info) || !ids.Add(info.Id))
            {
                continue;
            }

            if ((text is null) || (SvgSanitizer.FromFile(text, IdPrefix(prefix, info.Id)) is not { } svg))
            {
                issues.Add(Unreadable(info.Id));
                continue;
            }

            Report(info.Id, svg, issues);
            icons.Add(new DiagramIcon { Id = info.Id, Body = svg.Body, Width = svg.Width, Height = svg.Height, Name = info.Name ?? info.Id, Category = info.Category });
        }

        return new DiagramIconImportResult(new DiagramIconSet { Prefix = prefix, Icons = icons.ToImmutable() }, issues.ToImmutable());
    }

    private static DiagramIconInfo DefaultMap(string path)
    {
        var slash = path.LastIndexOf('/');
        var file = path[(slash + 1)..];
        var name = file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
        string? category = null;
        if (slash > 0)
        {
            var folder = path[..slash];
            category = folder[(folder.LastIndexOf('/') + 1)..];
        }

        return new DiagramIconInfo(name, name, category);
    }

    private static string IdPrefix(string prefix, string id) => $"{SvgSanitizer.SafeId(prefix)}-{SvgSanitizer.SafeId(id)}-";

    private static string? Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
        var buffer = new char[MaxFileLength + 1];
        var length = reader.ReadBlock(buffer, 0, buffer.Length);
        return length > MaxFileLength ? null : new string(buffer, 0, length);
    }

    private static void Report(string id, SanitizedSvg svg, ImmutableArray<DiagramIssue>.Builder issues)
    {
        if (svg.Removed.Count > 0)
        {
            issues.Add(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.RemovedSvgContent, [], [id, string.Join(", ", svg.Removed.Distinct(StringComparer.Ordinal))]));
        }

        if (svg.RejectedUrls.Count > 0)
        {
            issues.Add(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.RejectedUrl, [], [id, string.Join(", ", svg.RejectedUrls.Distinct(StringComparer.Ordinal))]));
        }
    }

    private static DiagramIssue Unreadable(string id) =>
        DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.RemovedSvgContent, [], [id, "the whole icon (not a readable SVG)"]);

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.Number) ? value.GetDouble() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True);
}
