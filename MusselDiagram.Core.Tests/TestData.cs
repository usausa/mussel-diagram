namespace MusselDiagram;

using System.Text.Json.Serialization;

public sealed record ServerData(string Name, string? ConfigId = null);

public sealed record SwitchData(IReadOnlyList<string> Outputs);

[JsonSerializable(typeof(ServerData))]
[JsonSerializable(typeof(SwitchData))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
