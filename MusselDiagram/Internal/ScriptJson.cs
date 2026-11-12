namespace MusselDiagram.Internal;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record ScriptOptions
{
    public required double MinZoom { get; init; }

    public required double MaxZoom { get; init; }

    public required string WheelMode { get; init; }

    public required bool PanEnabled { get; init; }

    public required bool ZoomEnabled { get; init; }

    public required bool SelectionEnabled { get; init; }

    public required bool BoxSelectionEnabled { get; init; }

    public required bool MoveEnabled { get; init; }

    public required bool ResizeEnabled { get; init; }

    public required bool ConnectEnabled { get; init; }

    public required int LivePreviewLimit { get; init; }

    public required bool Optimistic { get; init; }

    public required bool GridVisible { get; init; }

    public required string GridStyle { get; init; }

    public required double GridSize { get; init; }

    public required bool SnapToGrid { get; init; }

    public required bool SnapToElements { get; init; }

    public required double SnapTolerance { get; init; }

    public required double ConnectionRadius { get; init; }

    public required double QuickAddSpacing { get; init; }

    public string? LinkType { get; init; }

    public required bool DropEnabled { get; init; }

    public required bool TextEditEnabled { get; init; }

    public required bool TooltipEnabled { get; init; }

    public required bool HostSearch { get; init; }
}

internal sealed record ScriptBounds(double X, double Y, double Width, double Height);

internal sealed record ScriptExportOptions
{
    public required string Format { get; init; }

    public required double Margin { get; init; }

    public required bool Background { get; init; }

    public required bool Overlays { get; init; }

    public required bool Fonts { get; init; }

    public double Scale { get; init; } = 1;

    public double Quality { get; init; } = 0.92;
}

internal sealed record ScriptPort
{
    public required string Id { get; init; }

    public required string Side { get; init; }

    public required double Ratio { get; init; }

    public required double Offset { get; init; }

    public required string Direction { get; init; }

    public string? Kind { get; init; }

    public int? Max { get; init; }

    public IReadOnlyList<string>? Accepts { get; init; }

    public IReadOnlyList<string>? Rejects { get; init; }

    public string? LinkType { get; init; }

    public string? Label { get; init; }

    public bool Hidden { get; init; }
}

internal sealed record ScriptBox
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Kind { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required double W { get; init; }

    public required double H { get; init; }

    public string? Parent { get; init; }

    public IReadOnlyList<string>? Members { get; init; }

    public required bool Registered { get; init; }

    public bool Locked { get; init; }

    public required bool Selectable { get; init; }

    public required bool Movable { get; init; }

    public required bool Resizable { get; init; }

    public required bool Connectable { get; init; }

    public required bool Whole { get; init; }

    public bool Editable { get; init; }

    public IReadOnlyList<ScriptPort>? Ports { get; init; }

    public IReadOnlyDictionary<string, string>? Texts { get; init; }
}

internal sealed record ScriptSlot
{
    public required string Name { get; init; }

    public required bool Editable { get; init; }

    public required int MaxLines { get; init; }

    public string? Placeholder { get; init; }
}

internal sealed record ScriptCommand
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Keys { get; init; }

    public required bool Enabled { get; init; }

    public required bool Local { get; init; }
}

internal sealed record ScriptLink
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Source { get; init; }

    public string? SourcePort { get; init; }

    public required string Target { get; init; }

    public string? TargetPort { get; init; }

    public required double Stub { get; init; }

    public required string Form { get; init; }

    public required double SourceCut { get; init; }

    public required double TargetCut { get; init; }

    public required bool Selectable { get; init; }

    public required bool Connectable { get; init; }
}

internal sealed record ScriptType
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    public string? Category { get; init; }

    public string? CategoryLabel { get; init; }

    public IReadOnlyList<string>? Search { get; init; }

    public IReadOnlyList<ScriptSlot>? Slots { get; init; }

    public required bool Group { get; init; }

    public bool Overlay { get; init; }

    public bool Content { get; init; }

    public IReadOnlyList<double>? Padding { get; init; }

    public required double W { get; init; }

    public required double H { get; init; }

    public double? MinW { get; init; }

    public double? MinH { get; init; }

    public string? Icon { get; init; }

    public required string Shape { get; init; }

    public required bool Palette { get; init; }

    public IReadOnlyList<string>? Parents { get; init; }

    public IReadOnlyList<string>? Children { get; init; }

    public required bool TopLevel { get; init; }

    public bool Stay { get; init; }

    public int? Max { get; init; }

    public required bool Whole { get; init; }

    public required bool Connectable { get; init; }

    public IReadOnlyList<ScriptPort>? Ports { get; init; }
}

internal sealed record ScriptLinkType
{
    public required string Name { get; init; }

    public required string Form { get; init; }

    public required double Stub { get; init; }

    public required double SourceCut { get; init; }

    public required double TargetCut { get; init; }

    public bool AllowSelf { get; init; }

    public bool AllowDuplicate { get; init; }

    public required bool AllowCrossParent { get; init; }

    public required bool AllowCycles { get; init; }
}

internal sealed record ScriptTexts
{
    public required string SearchPlaceholder { get; init; }

    public required string NoMatches { get; init; }
}

internal sealed record ScriptSchema(IReadOnlyList<ScriptType> Types, IReadOnlyList<ScriptLinkType> LinkTypes, string? DefaultLinkType, IReadOnlyDictionary<string, string> Reasons, ScriptTexts Texts);

internal sealed record ScriptTable(long Version, IReadOnlyList<ScriptBox> Boxes, IReadOnlyList<ScriptLink> Links);

internal sealed record ScriptChanges(long Version, IReadOnlyList<ScriptBox> Boxes, IReadOnlyList<ScriptLink> Links, IReadOnlyList<string> Removed);

internal sealed record ScriptCommitResult(bool Accepted, long Version, string? Reason, int Rejections, string? ElementId = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Serialization, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScriptOptions))]
[JsonSerializable(typeof(ScriptBounds))]
[JsonSerializable(typeof(ScriptExportOptions))]
[JsonSerializable(typeof(ScriptSchema))]
[JsonSerializable(typeof(ScriptTable))]
[JsonSerializable(typeof(ScriptChanges))]
[JsonSerializable(typeof(ScriptCommitResult))]
[JsonSerializable(typeof(List<ScriptCommand>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class ScriptJsonContext : JsonSerializerContext;

internal static class ScriptJson
{
    public static JsonElement ToElement(ScriptOptions value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptOptions);

    public static JsonElement ToElement(ScriptBounds value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptBounds);

    public static JsonElement ToElement(ScriptExportOptions value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptExportOptions);

    public static JsonElement ToElement(ScriptSchema value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptSchema);

    public static JsonElement ToElement(ScriptTable value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptTable);

    public static JsonElement ToElement(ScriptChanges value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptChanges);

    public static JsonElement ToElement(ScriptCommitResult value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptCommitResult);

    public static JsonElement ToElement(IReadOnlyList<string> value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.IReadOnlyListString);
}
