namespace MusselDiagram.Internal;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record DiagramJsonDocument
{
    public string? Format { get; init; }

    public int Version { get; init; }

    public IReadOnlyList<DiagramJsonBox>? Groups { get; init; }

    public IReadOnlyList<DiagramJsonBox>? Nodes { get; init; }

    public IReadOnlyList<DiagramJsonLink>? Links { get; init; }

    public IReadOnlyList<DiagramJsonSharedItem>? SharedItems { get; init; }
}

internal sealed record DiagramJsonBox
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public string? Kind { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Width { get; init; }

    public double? Height { get; init; }

    public string? Parent { get; init; }

    public string? Label { get; init; }

    public IReadOnlyDictionary<string, string?>? Texts { get; init; }

    public string? Icon { get; init; }

    public JsonElement? Data { get; init; }

    public bool Locked { get; init; }

    public IReadOnlyList<string>? Permissions { get; init; }

    public IReadOnlyList<string>? Members { get; init; }
}

internal sealed record DiagramJsonLink
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public DiagramJsonEnd? Source { get; init; }

    public DiagramJsonEnd? Target { get; init; }

    public IReadOnlyList<IReadOnlyList<double>>? Route { get; init; }

    public JsonElement? Data { get; init; }

    public bool Locked { get; init; }

    public IReadOnlyList<string>? Permissions { get; init; }
}

internal sealed record DiagramJsonEnd
{
    public string? Element { get; init; }

    public string? Port { get; init; }
}

internal sealed record DiagramJsonSharedItem
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public string? Label { get; init; }

    public JsonElement? Data { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(DiagramJsonDocument))]
internal sealed partial class DiagramJsonContext : JsonSerializerContext;
