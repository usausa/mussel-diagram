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
                !root.TryGetProperty("prefix", out var prefixValue) || (prefixValue.ValueKind != JsonValueKind.String) || (prefixValue.GetString() is not { Length: > 0 } prefix) ||
                !root.TryGetProperty("icons", out var iconValues) || (iconValues.ValueKind != JsonValueKind.Object))
            {
                throw new DiagramException("The stream is not an Iconify icon set.");
            }

            var width = Size(root, "width") ?? 16;
            var height = Size(root, "height") ?? 16;
            var categories = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("categories", out var categoryValues) && (categoryValues.ValueKind == JsonValueKind.Object))
            {
                foreach (var category in categoryValues.EnumerateObject())
                {
                    if (category.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var name in category.Value.EnumerateArray())
                        {
                            if (name.ValueKind == JsonValueKind.String)
                            {
                                categories.TryAdd(name.GetString()!, category.Name);
                            }
                        }
                    }
                }
            }

            var icons = ImmutableArray.CreateBuilder<DiagramIcon>();
            var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var icon in iconValues.EnumerateObject())
            {
                if (ids.Add(icon.Name))
                {
                    Add(icon.Name, new AliasChain(icon.Value));
                }
            }

            if (root.TryGetProperty("aliases", out var aliases) && (aliases.ValueKind == JsonValueKind.Object))
            {
                var iconsByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var icon in iconValues.EnumerateObject())
                {
                    iconsByName.TryAdd(icon.Name, icon.Value);
                }

                var aliasesByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var alias in aliases.EnumerateObject())
                {
                    aliasesByName.TryAdd(alias.Name, alias.Value);
                }

                var resolved = new Dictionary<string, AliasChain?>(StringComparer.Ordinal);
                var walk = new List<(string Name, JsonElement Value)>();
                var walking = new HashSet<string>(StringComparer.Ordinal);
                foreach (var alias in aliases.EnumerateObject())
                {
                    if (!ids.Add(alias.Name))
                    {
                        continue;
                    }

                    if (Resolve(alias.Name, alias.Value) is { } chain)
                    {
                        Add(alias.Name, chain);
                    }
                    else
                    {
                        issues.Add(Unreadable(alias.Name));
                    }
                }

                AliasChain? Resolve(string name, JsonElement value)
                {
                    walk.Clear();
                    walking.Add(name);
                    AliasChain? result = null;
                    while (true)
                    {
                        walk.Add((name, value));
                        if ((value.ValueKind != JsonValueKind.Object) || !value.TryGetProperty("parent", out var parent) || (parent.ValueKind != JsonValueKind.String))
                        {
                            break;
                        }

                        name = parent.GetString()!;
                        if (iconsByName.TryGetValue(name, out var icon))
                        {
                            result = new AliasChain(icon);
                            break;
                        }

                        if (resolved.TryGetValue(name, out var known))
                        {
                            result = known;
                            break;
                        }

                        if (!aliasesByName.TryGetValue(name, out value) || !walking.Add(name))
                        {
                            break;
                        }
                    }

                    for (var i = walk.Count - 1; i >= 0; i--)
                    {
                        walking.Remove(walk[i].Name);
                        result = result?.For(walk[i].Value);
                        resolved.TryAdd(walk[i].Name, result);
                    }

                    return result;
                }
            }

            return new DiagramIconImportResult(new DiagramIconSet { Prefix = prefix, Icons = icons.ToImmutable() }, issues.ToImmutable());

            void Add(string id, AliasChain chain)
            {
                var value = chain.Icon;
                if ((value.ValueKind != JsonValueKind.Object) || !value.TryGetProperty("body", out var bodyValue) || (bodyValue.ValueKind != JsonValueKind.String))
                {
                    issues.Add(Unreadable(id));
                    return;
                }

                var body = bodyValue.GetString()!;

                var iconWidth = chain.Width ?? Size(value, "width") ?? width;
                var iconHeight = chain.Height ?? Size(value, "height") ?? height;
                var left = chain.Left ?? Number(value, "left") ?? 0;
                var top = chain.Top ?? Number(value, "top") ?? 0;
                var turns = (Number(value, "rotate") ?? 0) + chain.Turns;
                var horizontalFlip = Flag(value, "hFlip") ^ chain.HorizontalFlip;
                var verticalFlip = Flag(value, "vFlip") ^ chain.VerticalFlip;

                var rotate = (int)(turns % 4);
                if (rotate < 0)
                {
                    rotate += 4;
                }

                var transforms = new List<string>();
                if (rotate != 0)
                {
                    var (x, y) = rotate switch
                    {
                        1 => (iconHeight / 2, iconHeight / 2),
                        2 => (iconWidth / 2, iconHeight / 2),
                        _ => (iconWidth / 2, iconWidth / 2)
                    };
                    transforms.Add($"rotate({SvgSanitizer.Number(rotate * 90)} {SvgSanitizer.Number(x)} {SvgSanitizer.Number(y)})");
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
                    body = $"<g transform=\"{String.Join(' ', transforms)}\">{body}</g>";
                }

                if ((rotate % 2) == 1)
                {
                    (iconWidth, iconHeight) = (iconHeight, iconWidth);
                }

                if (SvgSanitizer.FromBody(body, SvgSanitizer.IdPrefix(prefix, id), iconWidth, iconHeight) is not { } svg)
                {
                    issues.Add(Unreadable(id));
                    return;
                }

                Report(id, svg, issues);
                icons.Add(SvgSanitizer.MarkImported(new DiagramIcon { Id = id, Body = svg.Body, Width = svg.Width, Height = svg.Height, Name = id, Category = categories.GetValueOrDefault(id) }));
            }
        }
    }

    public static DiagramIconImportResult FromZip(Stream stream, string prefix, Func<string, DiagramIconInfo?>? map = null)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        }
        catch (InvalidDataException e)
        {
            throw new DiagramException("The stream is not a zip file.", e);
        }

        using (archive)
        {
            List<ZipArchiveEntry> entries;
            try
            {
                entries = [.. archive.Entries];
            }
            catch (InvalidDataException e)
            {
                throw new DiagramException("The stream is not a zip file.", e);
            }

            return ReadFiles(
                entries
                    .Where(static x => x.FullName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    .Select(static x => (x.FullName.Replace('\\', '/'), (Func<string?>)(() => Read(x)))),
                prefix,
                map);
        }
    }

    public static DiagramIconImportResult FromFolder(string path, string prefix, Func<string, DiagramIconInfo?>? map = null) =>
        ReadFiles(
            Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                .Where(static x => x.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                .Select(file => (Path.GetRelativePath(path, file).Replace('\\', '/'), (Func<string?>)(() => ReadFile(file)))),
            prefix,
            map);

    public static DiagramIconSet FromFiles(IEnumerable<string> paths, Func<string, CancellationToken, Task<string?>> read, string prefix, Func<string, DiagramIconInfo?>? map = null)
    {
        map ??= DefaultMap;
        var icons = ImmutableArray.CreateBuilder<DiagramIcon>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths.Select(static x => x.Replace('\\', '/')).Where(static x => x.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            if ((map(path) is not { } info) || !ids.Add(info.Id))
            {
                continue;
            }

            icons.Add(new DiagramIcon { Id = info.Id, Name = info.Name ?? info.Id, Category = info.Category, Load = cancellationToken => read(path, cancellationToken) });
        }

        return new DiagramIconSet { Prefix = prefix, Icons = icons.ToImmutable() };
    }

    public static ImmutableArray<DiagramNodeType> CreateNodeTypes(DiagramIconSet set, DiagramNodeType baseType, Func<DiagramIcon, bool>? filter = null) =>
        [.. set.Icons.Where(x => filter?.Invoke(x) ?? true).Select(icon => (DiagramNodeType)TypeFor(baseType, set.Prefix, icon))];

    public static ImmutableArray<DiagramGroupType> CreateGroupTypes(DiagramIconSet set, DiagramGroupType baseType, Func<DiagramIcon, bool>? filter = null) =>
        [.. set.Icons.Where(x => filter?.Invoke(x) ?? true).Select(icon => (DiagramGroupType)TypeFor(baseType, set.Prefix, icon))];

    private static DiagramBoxType TypeFor(DiagramBoxType baseType, string prefix, DiagramIcon icon) =>
        baseType with
        {
            Name = $"{prefix}:{icon.Id}",
            DisplayName = icon.Name ?? icon.Id,
            Category = icon.Category ?? baseType.Category,
            SearchWords = icon.SearchWords,
            Look = baseType.Look with { Icon = $"{prefix}:{icon.Id}" }
        };

    private static DiagramIconImportResult ReadFiles(IEnumerable<(string Path, Func<string?> Read)> files, string prefix, Func<string, DiagramIconInfo?>? map)
    {
        map ??= DefaultMap;
        var icons = ImmutableArray.CreateBuilder<DiagramIcon>();
        var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, read) in files.OrderBy(static x => x.Path, StringComparer.Ordinal))
        {
            if ((map(path) is not { } info) || !ids.Add(info.Id))
            {
                continue;
            }

            if ((read() is not { } text) || (SvgSanitizer.FromFile(text, SvgSanitizer.IdPrefix(prefix, info.Id)) is not { } svg))
            {
                issues.Add(Unreadable(info.Id));
                continue;
            }

            Report(info.Id, svg, issues);
            icons.Add(SvgSanitizer.MarkImported(new DiagramIcon { Id = info.Id, Body = svg.Body, Width = svg.Width, Height = svg.Height, Name = info.Name ?? info.Id, Category = info.Category }));
        }

        return new DiagramIconImportResult(new DiagramIconSet { Prefix = prefix, Icons = icons.ToImmutable() }, issues.ToImmutable());
    }

    private static DiagramIconInfo DefaultMap(string path)
    {
#if NET11_0_OR_GREATER
        var slash = path.LastIndexOf('/', StringComparison.Ordinal);
#else
        var slash = path.LastIndexOf('/');
#endif
        var file = path[(slash + 1)..];
        var name = file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
        string? category = null;
        if (slash > 0)
        {
            var folder = path[..slash];
#if NET11_0_OR_GREATER
            category = folder[(folder.LastIndexOf('/', StringComparison.Ordinal) + 1)..];
#else
            category = folder[(folder.LastIndexOf('/') + 1)..];
#endif
        }

        return new DiagramIconInfo(name, name, category);
    }

    private static string? Read(ZipArchiveEntry entry)
    {
        try
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true, 4096, false);
            return Read(reader);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? ReadFile(string file)
    {
        try
        {
            using var reader = new StreamReader(file, Encoding.UTF8, true);
            return Read(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Read(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int length;
        while ((length = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (text.Length + length > SvgSanitizer.MaxFileLength)
            {
                return null;
            }

            text.Append(buffer, 0, length);
        }

        return text.ToString();
    }

    private static void Report(string id, SanitizedSvg svg, ImmutableArray<DiagramIssue>.Builder issues)
    {
        if (svg.Removed.Count > 0)
        {
            issues.Add(MusselDiagramOptions.Default.Localize(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.RemovedSvgContent, [], [id, String.Join(", ", svg.Removed.Distinct(StringComparer.Ordinal))])));
        }

        if (svg.RejectedUrls.Count > 0)
        {
            issues.Add(MusselDiagramOptions.Default.Localize(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.RejectedUrl, [], [id, String.Join(", ", svg.RejectedUrls.Distinct(StringComparer.Ordinal))])));
        }
    }

    private static DiagramIssue Unreadable(string id) =>
        MusselDiagramOptions.Default.Localize(DiagramValidator.Issue(DiagramIssueSeverity.Warning, DiagramIssueKind.UnreadableIcon, [], [id]));

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.Number) && value.TryGetDouble(out var number) && Double.IsFinite(number) ? number : null;

    private static double? Size(JsonElement element, string name) => (Number(element, name) is { } size) && (size > 0) ? size : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True);

    private sealed record AliasChain(JsonElement Icon, double Turns = 0, bool HorizontalFlip = false, bool VerticalFlip = false, double? Width = null, double? Height = null, double? Left = null, double? Top = null)
    {
        public AliasChain For(JsonElement alias) => this with
        {
            Turns = Turns + (Number(alias, "rotate") ?? 0),
            HorizontalFlip = HorizontalFlip ^ Flag(alias, "hFlip"),
            VerticalFlip = VerticalFlip ^ Flag(alias, "vFlip"),
            Width = Size(alias, "width") ?? Width,
            Height = Size(alias, "height") ?? Height,
            Left = Number(alias, "left") ?? Left,
            Top = Number(alias, "top") ?? Top
        };
    }
}
