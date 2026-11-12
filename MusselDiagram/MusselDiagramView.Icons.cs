namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private IconSymbols? symbols;

    private RenderContext? iconContext;

    private DiagramSnapshot? iconSnapshot;

    private IconSymbols Symbols => symbols ??= new IconSymbols(InvokeAsync);

    internal void WantIcon(string reference) => Symbols.Want(reference);

    internal void WantIconIds(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (RenderContext.FindIconById(id) is { } entry)
            {
                Symbols.Want(entry.Reference);
            }
        }
    }

    private void WantIcons(DiagramChangedEventArgs? change)
    {
        if (!ReferenceEquals(iconContext, RenderContext))
        {
            iconContext = RenderContext;
            iconSnapshot = null;
            Symbols.Use(RenderContext);
        }

        var snapshot = change?.After ?? Snapshot;
        if (ReferenceEquals(iconSnapshot, snapshot))
        {
            return;
        }

        var boxes = (change is { IsReset: false }) && ReferenceEquals(iconSnapshot, change.Before)
            ? change.Added.Concat(change.Updated).Select(snapshot.Find).OfType<DiagramBox>()
            : snapshot.Groups.Cast<DiagramBox>().Concat(snapshot.Nodes);
        iconSnapshot = snapshot;
        var index = RenderContext.Index;
        foreach (var box in boxes)
        {
            if ((box.Icon ?? (index.FindBoxType(box) is { } type ? index.LookOf(type).Icon : null)) is { } icon)
            {
                Symbols.Want(icon);
            }
        }
    }

    private void WantBadgeIcons()
    {
        foreach (var badges in overlays.Badges.Values)
        {
            foreach (var item in badges.Items)
            {
                if (item.Badge.Icon is { } icon)
                {
                    Symbols.Want(icon);
                }
            }
        }
    }
}
