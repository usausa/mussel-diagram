namespace Example.Client.Models;

public sealed class WorkflowRun
{
    private readonly List<string> steps = [];

    private readonly Dictionary<string, List<string>> attachments = new(StringComparer.Ordinal);

    private readonly List<(string Id, string Source, int Count)> links = [];

    private readonly HashSet<string> others = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> runs = new(StringComparer.Ordinal);

    public WorkflowRun(Diagram diagram)
    {
        var snapshot = diagram.Snapshot;
        var starts = diagram.Schema.NodeTypes.Where(static x => x.Start).Select(static x => x.Name).ToHashSet(StringComparer.Ordinal);
        var queue = new Queue<string>(snapshot.Nodes.Where(x => starts.Contains(x.Type)).OrderBy(static x => x.Bounds.Y).ThenBy(static x => x.Bounds.X).Select(static x => x.Id));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.TryDequeue(out var id))
        {
            if (!visited.Add(id) || (snapshot.Find(id) is not DiagramNode node))
            {
                continue;
            }

            steps.Add(id);
            foreach (var link in snapshot.GetLinks(id, DiagramLinkDirection.Out))
            {
                if (string.Equals(link.Type, "ai", StringComparison.Ordinal))
                {
                    if (!attachments.TryGetValue(id, out var list))
                    {
                        list = [];
                        attachments[id] = list;
                    }

                    list.Add(link.Target.ElementId);
                    continue;
                }

                if (string.Equals(node.Type, "branch", StringComparison.Ordinal) && !string.Equals(link.Source.PortId, "true", StringComparison.Ordinal))
                {
                    continue;
                }

                links.Add((link.Id, id, starts.Contains(node.Type) ? 1 : 12));
                queue.Enqueue(link.Target.ElementId);
            }
        }

        var reached = steps.Concat(attachments.Values.SelectMany(static x => x)).ToHashSet(StringComparer.Ordinal);
        foreach (var node in snapshot.Nodes.Where(x => !reached.Contains(x.Id)))
        {
            others.Add(node.Id);
        }
    }

    public int Step { get; private set; } = -1;

    public bool Running => (Step >= 0) && (Step < steps.Count);

    public void Start()
    {
        runs.Clear();
        Step = 0;
    }

    public void Advance()
    {
        if (Running)
        {
            runs[steps[Step]] = 1;
            Step++;
        }
    }

    public bool Rerun(string id)
    {
        if (!runs.TryGetValue(id, out var count))
        {
            return false;
        }

        runs[id] = count + 1;
        return true;
    }

    public Dictionary<string, DiagramOverlay> Overlays()
    {
        var result = new Dictionary<string, DiagramOverlay>(StringComparer.Ordinal);
        if (Step < 0)
        {
            return result;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var id = steps[i];
            var overlay = Overlay(id, i);
            result[id] = overlay;
            foreach (var attached in attachments.GetValueOrDefault(id) ?? [])
            {
                result[attached] = overlay with { Badges = [] };
            }
        }

        foreach (var (id, source, count) in links)
        {
            if (steps.IndexOf(source) < Step)
            {
                result[id] = new DiagramOverlay { States = ["flowed"], Badges = [new DiagramBadge { Text = string.Create(CultureInfo.InvariantCulture, $"{count} 件") }] };
            }
        }

        foreach (var id in others)
        {
            result[id] = new DiagramOverlay { Style = new DiagramRenderOverride { Opacity = 0.45 } };
        }

        return result;
    }

    private DiagramOverlay Overlay(string id, int index)
    {
        if (index < Step)
        {
            return new DiagramOverlay
            {
                States = ["done"],
                Badges =
                [
                    new DiagramBadge { Kind = WorkflowDiagrams.StatusBadge, Text = "完了", Tone = DiagramBadgeTone.Success },
                    new DiagramBadge { Kind = WorkflowDiagrams.RunsBadge, Text = runs.GetValueOrDefault(id, 1).ToString(CultureInfo.InvariantCulture), Tooltip = "もう一度実行" }
                ]
            };
        }

        return index == Step
            ? new DiagramOverlay { States = ["running"], Badges = [new DiagramBadge { Kind = WorkflowDiagrams.StatusBadge, Text = "実行中", Tone = DiagramBadgeTone.Info }] }
            : new DiagramOverlay { Style = new DiagramRenderOverride { Opacity = 0.45 } };
    }
}
