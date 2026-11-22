namespace MusselDiagram;

using System.Text.Json.Serialization;

public sealed record ServerData(string Name, string? ConfigId = null);

public sealed record SwitchData(IReadOnlyList<string> Outputs);

public sealed record PortData(int Port)
{
    public int Port { get; } = Port >= 0 ? Port : throw new FormatException();
}

[JsonSerializable(typeof(ServerData))]
[JsonSerializable(typeof(SwitchData))]
[JsonSerializable(typeof(PortData))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
