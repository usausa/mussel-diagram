namespace MusselDiagram.Internal;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record ScriptOptions(
    double MinZoom,
    double MaxZoom,
    string WheelMode,
    bool PanEnabled,
    bool ZoomEnabled,
    bool SelectionEnabled,
    bool MoveEnabled,
    int LivePreviewLimit,
    bool Optimistic);

internal sealed record ScriptBounds(double X, double Y, double Width, double Height);

internal sealed record ScriptPort
{
    public required string Id { get; init; }

    public required string Side { get; init; }

    public required double Ratio { get; init; }

    public required double Offset { get; init; }
}

internal sealed record ScriptBox
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required double W { get; init; }

    public required double H { get; init; }

    public string? Parent { get; init; }

    public IReadOnlyList<string>? Members { get; init; }

    public required bool Selectable { get; init; }

    public required bool Movable { get; init; }

    public IReadOnlyList<ScriptPort>? Ports { get; init; }
}

internal sealed record ScriptLink
{
    public required string Id { get; init; }

    public required string Source { get; init; }

    public string? SourcePort { get; init; }

    public required string Target { get; init; }

    public string? TargetPort { get; init; }

    public required double Stub { get; init; }

    public required bool Straight { get; init; }

    public required bool Selectable { get; init; }
}

internal sealed record ScriptTable(long Version, IReadOnlyList<ScriptBox> Boxes, IReadOnlyList<ScriptLink> Links);

internal sealed record ScriptChanges(long Version, IReadOnlyList<ScriptBox> Boxes, IReadOnlyList<ScriptLink> Links, IReadOnlyList<string> Removed);

internal sealed record ScriptCommitResult(bool Accepted, long Version, string? Reason, int Rejections);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Serialization, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScriptOptions))]
[JsonSerializable(typeof(ScriptBounds))]
[JsonSerializable(typeof(ScriptTable))]
[JsonSerializable(typeof(ScriptChanges))]
[JsonSerializable(typeof(ScriptCommitResult))]
internal sealed partial class ScriptJsonContext : JsonSerializerContext;

internal static class ScriptJson
{
    public static JsonElement ToElement(ScriptOptions value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptOptions);

    public static JsonElement ToElement(ScriptBounds value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptBounds);

    public static JsonElement ToElement(ScriptTable value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptTable);

    public static JsonElement ToElement(ScriptChanges value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptChanges);

    public static JsonElement ToElement(ScriptCommitResult value) => JsonSerializer.SerializeToElement(value, ScriptJsonContext.Default.ScriptCommitResult);
}
