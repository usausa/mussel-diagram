namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private OverlayIndex overlays = OverlayIndex.Empty;

    private IReadOnlyDictionary<string, DiagramOverlay>? renderedOverlays;

    private IReadOnlyList<DiagramIssue>? renderedIssues;

    private IReadOnlyList<DiagramBadgeKind>? renderedBadgeKinds;

    [Parameter]
    public IReadOnlyList<DiagramIssue>? Issues { get; set; }

    [Parameter]
    public IReadOnlyDictionary<string, DiagramOverlay>? Overlays { get; set; }

    [Parameter]
    public IReadOnlyList<DiagramBadgeKind>? BadgeKinds { get; set; }

    [Parameter]
    public EventCallback<DiagramBadgeClickedEventArgs> BadgeClicked { get; set; }

    private RenderFragment Badges => builder =>
    {
        if (overlays.Badges.Count == 0)
        {
            return;
        }

        foreach (var group in Snapshot.Groups)
        {
            if (IsShown(group.Id, group.Bounds))
            {
                AddBadges(builder, group);
            }
        }

        foreach (var link in Snapshot.Links)
        {
            if (IsShown(link.Id, RouteBounds(link)))
            {
                AddBadges(builder, link);
            }
        }

        foreach (var node in Snapshot.Nodes)
        {
            if (IsShown(node.Id, node.Bounds))
            {
                AddBadges(builder, node);
            }
        }
    };

    internal async Task OnBadgeClickedAsync(string id, int index)
    {
        if ((overlays.Badges.GetValueOrDefault(id) is not { } badges) || (index < 0) || (index >= badges.Items.Length) || !badges.Items[index].Clickable || !Snapshot.Contains(id))
        {
            return;
        }

        await BadgeClicked.InvokeAsync(new DiagramBadgeClickedEventArgs(id, badges.Items[index].Badge)).ConfigureAwait(true);
    }

    private void RefreshOverlays()
    {
        if (ReferenceEquals(Overlays, renderedOverlays) && ReferenceEquals(Issues, renderedIssues) && ReferenceEquals(BadgeKinds, renderedBadgeKinds))
        {
            return;
        }

        renderedOverlays = Overlays;
        renderedIssues = Issues;
        renderedBadgeKinds = BadgeKinds;
        overlays = OverlayIndex.Build(Overlays, Issues, BadgeKinds, overlays);
        commands?.Refresh();
    }

    private void AddBadges(RenderTreeBuilder builder, DiagramElement element)
    {
        if (overlays.Badges.GetValueOrDefault(element.Id) is not { } badges)
        {
            return;
        }

        builder.OpenComponent<DiagramBadgesView>(0);
        builder.SetKey(element.Id);
        builder.AddComponentParameter(1, nameof(DiagramBadgesView.Element), element);
        builder.AddComponentParameter(2, nameof(DiagramBadgesView.Badges), badges);
        builder.AddComponentParameter(3, nameof(DiagramBadgesView.Context), RenderContext);
        builder.CloseComponent();
    }

    private bool HasIssues() => overlays.IssueIds.Any(Snapshot.Contains);

    private async Task ShowIssueAsync(bool forward)
    {
        var ordered = overlays.IssueIds
            .Select(x => (Id: x, Bounds: BoundsOf(x)))
            .Where(static x => x.Bounds is not null)
            .OrderBy(static x => x.Bounds!.Value.Y)
            .ThenBy(static x => x.Bounds!.Value.X)
            .ThenBy(static x => x.Id, StringComparer.Ordinal)
            .ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        var current = SelectedIds.Count == 1 ? ordered.FindIndex(x => string.Equals(x.Id, SelectedIds[0], StringComparison.Ordinal)) : -1;
        var next = current < 0
            ? (forward ? 0 : ordered.Count - 1)
            : (current + (forward ? 1 : ordered.Count - 1)) % ordered.Count;
        var (id, bounds) = ordered[next];
        await SelectAsync([id]).ConfigureAwait(true);
        if ((Viewport?.VisibleBounds is not { } visible) || !Contains(visible, bounds!.Value))
        {
            await CenterOnAsync(id).ConfigureAwait(true);
        }
    }

    private DiagramRect? BoundsOf(string id) => Snapshot.Find(id) switch
    {
        DiagramBox box => box.Bounds,
        DiagramLink { Route.IsDefaultOrEmpty: false } link => RouteBounds(link),
        _ => null
    };

    private static bool Contains(DiagramRect outer, DiagramRect inner) =>
        (inner.X >= outer.X) && (inner.Y >= outer.Y) && (inner.Right <= outer.Right) && (inner.Bottom <= outer.Bottom);
}
