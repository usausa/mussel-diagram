namespace MusselDiagram;

using System.Text.Json;
using System.Text.Json.Serialization;

using MusselDiagram.Internal;

using static MusselDiagram.TestDiagrams;

public sealed class DeepTreeTests
{
    private const int Depth = 50_000;

    private const int Leaves = 10_000;

    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Fact(Timeout = 30_000)]
    public Task DeepChainsLoadMoveAndDeleteQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(Leaves));

                diagram.Move(["g0"], new DiagramPoint(10, 0));
                diagram.SetParent(Enumerable.Range(0, Leaves).Select(static i => $"n{i}"), $"g{Depth - 1}");
                diagram.Delete(["g0"]);

                Assert.Empty(diagram.Snapshot.Groups);
                Assert.Empty(diagram.Snapshot.Nodes);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task CommandsOnADeepChainRunQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(0));
                var snapshot = diagram.Snapshot;
                var all = Enumerable.Range(0, Depth).Select(static i => $"g{Depth - 1 - i}").ToList();

                var deletable = Deletion.Deletable(snapshot, diagram.Index, all);
                var alignable = Arrangement.CanAlign(snapshot, diagram.Index, all, 2);
                var (pasted, reason) = Clipboard.Paste(snapshot, diagram.Index, Clipboard.Copy(snapshot, diagram.Index, all)!, new DiagramPoint(20, 20));

                Assert.Equal(Depth, deletable.Count);
                Assert.False(alignable);
                Assert.Null(reason);
                Assert.Equal(Depth, pasted!.Elements.Length);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task ReplacingEveryGroupOfADeepChainRunsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(0));
                var groups = diagram.Snapshot.Groups.ToList();

                diagram.Write(null, DiagramChangeOrigin.User, editor =>
                {
                    foreach (var group in groups)
                    {
                        editor.Replace(group with { Locked = true });
                    }
                });

                Assert.All(diagram.Snapshot.Groups, static x => Assert.True(x.Locked));
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task ASkippedWriteAfterARecordedOneOnADeepChainRunsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(0));
                var before = ((DiagramBox)diagram.Snapshot.Find("g1")!).Bounds.X;

                diagram.Move(["g1"], new DiagramPoint(10, 0));
                diagram.Move(["g0"], new DiagramPoint(10, 0), new DiagramWriteOptions { History = DiagramHistoryMode.Skip });

                Assert.Equal(before + 20, ((DiagramBox)diagram.Snapshot.Find("g1")!).Bounds.X);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task UngroupingADeepChainRunsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(0));

                var result = Grouping.Ungroup(diagram.Snapshot, diagram.Index, diagram.Snapshot.Groups.Select(static x => x.Id));
                Assert.Null(result.Reason);
                diagram.Write(null, DiagramChangeOrigin.User, result.Edit!);

                Assert.Empty(diagram.Snapshot.Groups);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task UngroupingEveryOtherGroupOfADeepChainRunsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(0));
                var odd = Enumerable.Range(0, Depth / 2).Select(static i => $"g{(i * 2) + 1}").ToList();

                var result = Grouping.Ungroup(diagram.Snapshot, diagram.Index, odd);
                Assert.Null(result.Reason);
                diagram.Write(null, DiagramChangeOrigin.User, result.Edit!);

                Assert.Equal(Depth / 2, diagram.Snapshot.Groups.Length);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task MovingManyNodesIntoADeepGroupWithoutHistoryRunsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var diagram = new Diagram(Schema);
                diagram.LoadJson(Chain(Leaves));
                diagram.Move(["g0"], new DiagramPoint(10, 0));

                diagram.SetParent(Enumerable.Range(0, Leaves).Select(static i => $"n{i}"), $"g{Depth - 1}", new DiagramWriteOptions { History = DiagramHistoryMode.Skip });

                Assert.All(diagram.Snapshot.Nodes, static x => Assert.Equal($"g{Depth - 1}", x.ParentId));
            },
            TestContext.Current.CancellationToken);
    }

    private static string Chain(int leaves) =>
        JsonSerializer.Serialize(
            new
            {
                format = "mussel-diagram",
                version = 1,
                groups = Enumerable.Range(0, Depth).Select(static i => new { id = $"g{i}", type = "vpc", kind = "tree", x = 0, y = 0, width = 400, height = 300, parent = i == 0 ? null : $"g{i - 1}" }),
                nodes = Enumerable.Range(0, leaves).Select(static i => new { id = $"n{i}", type = "server", x = 0, y = 0, width = 100, height = 50 })
            },
            Options);
}
