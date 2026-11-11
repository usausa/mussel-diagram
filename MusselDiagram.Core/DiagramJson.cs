namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public sealed record DiagramLoadResult(DiagramSnapshot Snapshot, ImmutableArray<DiagramIssue> Issues)
{
    internal IReadOnlyList<PendingRoute> PendingRoutes { get; init; } = [];
}

public static class DiagramJson
{
    public static void Write(Stream stream, DiagramSnapshot snapshot, DiagramSchema schema) =>
        DiagramJsonFormat.Write(stream, snapshot, SchemaIndex.For(schema));

    public static Task WriteAsync(Stream stream, DiagramSnapshot snapshot, DiagramSchema schema, CancellationToken cancellationToken = default) =>
        DiagramJsonFormat.WriteAsync(stream, snapshot, SchemaIndex.For(schema), cancellationToken);

    public static DiagramLoadResult Read(Stream stream, DiagramSchema schema) =>
        DiagramJsonFormat.Read(DiagramJsonFormat.Parse(stream), SchemaIndex.For(schema), 0);

    public static async Task<DiagramLoadResult> ReadAsync(Stream stream, DiagramSchema schema, CancellationToken cancellationToken = default) =>
        DiagramJsonFormat.Read(await DiagramJsonFormat.ParseAsync(stream, cancellationToken).ConfigureAwait(false), SchemaIndex.For(schema), 0);
}
