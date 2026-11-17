namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed partial class LookPanel : IDisposable
{
    private const string Keep = "";

    private static readonly (string Value, string Text)[] Colors =
    [
        ("#FFFFFF", "白"),
        ("#212121", "黒"),
        ("#757575", "灰"),
        ("#E53935", "赤"),
        ("#FB8C00", "橙"),
        ("#43A047", "緑"),
        ("#1E88E5", "青"),
        ("#8E24AA", "紫")
    ];

    private static readonly (string Value, string Text)[] Transparencies =
    [
        (string.Empty, "0%"),
        ("BF", "25%"),
        ("80", "50%"),
        ("40", "75%")
    ];

    private static readonly string[] Widths = ["1", "1.5", "2", "3", "4"];

    private static readonly (DiagramDash Value, string Text)[] Dashes =
    [
        (DiagramDash.Solid, "実線"),
        (DiagramDash.Dashed, "破線"),
        (DiagramDash.Dotted, "点線")
    ];

    private static readonly (DiagramMarker Value, string Text)[] Markers =
    [
        (DiagramMarker.None, "なし"),
        (DiagramMarker.Arrow, "矢印"),
        (DiagramMarker.OpenArrow, "開いた矢印"),
        (DiagramMarker.Circle, "円"),
        (DiagramMarker.Diamond, "ひし形")
    ];

    private static readonly (DiagramLineForm Value, string Text)[] Forms =
    [
        (DiagramLineForm.Orthogonal, "直交"),
        (DiagramLineForm.Straight, "直線"),
        (DiagramLineForm.Curved, "曲線")
    ];

    private static readonly DiagramLinkLook EmptyLinkLook = new();

    private static readonly DiagramBoxLook EmptyBoxLook = new();

    private Diagram? subscribed;

    [Parameter]
    [EditorRequired]
    public Diagram Diagram { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<string> Selection { get; set; } = [];

    private DiagramLink[] Links => [.. Selection.Select(Diagram.Find).OfType<DiagramLink>()];

    private DiagramBox[] Boxes => [.. Selection.Select(Diagram.Find).OfType<DiagramBox>()];

    public void Dispose() => Subscribe(null);

    protected override void OnParametersSet() => Subscribe(Diagram);

    private static string Code(int index) => index.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<(string Code, string Text)> Items<T>(IEnumerable<(T Value, string Text)> items) => items.Select(static (x, i) => (Code(i), x.Text));

    private static string CodeOf<T>(T? value, (T Value, string Text)[] items)
        where T : struct
    {
        for (var i = 0; (value is { } known) && (i < items.Length); i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i].Value, known))
            {
                return Code(i);
            }
        }

        return Keep;
    }

    private static T? ValueOf<T>(string? code, (T Value, string Text)[] items)
        where T : struct =>
        int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && (index < items.Length) ? items[index].Value : null;

    private static string WidthOf(double? width) => width?.ToString(CultureInfo.InvariantCulture) ?? Keep;

    private static double? ReadWidth(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null;

    private static string? ReadColor(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? ColorOf(string? fill) => HasAlpha(fill) ? fill[..7] : fill;

    private static string AlphaOf(string? fill) => HasAlpha(fill) ? fill[7..].ToUpperInvariant() : Keep;

    private static string? WithAlpha(string? color, string? alpha) =>
        (color is ['#', _, _, _, _, _, _]) && !string.IsNullOrEmpty(alpha) ? color + alpha : color;

    private static bool HasAlpha([NotNullWhen(true)] string? fill) => fill is ['#', _, _, _, _, _, _, _, _];

    private void ChangeLinks(Func<DiagramLinkLook, DiagramLinkLook> change)
    {
        using var update = Diagram.BeginUpdate();
        foreach (var link in Links)
        {
            var look = change(link.Look ?? EmptyLinkLook);
            Diagram.Replace(link with { Look = look == EmptyLinkLook ? null : look });
        }
    }

    private void ChangeBoxes(Func<DiagramBoxLook, DiagramBoxLook> change)
    {
        using var update = Diagram.BeginUpdate();
        foreach (var box in Boxes)
        {
            var look = change(box.Look ?? EmptyBoxLook);
            Diagram.Replace(box with { Look = look == EmptyBoxLook ? null : look });
        }
    }

    private void Subscribe(Diagram? diagram)
    {
        if (ReferenceEquals(subscribed, diagram))
        {
            return;
        }

        if (subscribed is not null)
        {
            subscribed.Changed -= OnChanged;
        }

        subscribed = diagram;
        if (subscribed is not null)
        {
            subscribed.Changed += OnChanged;
        }
    }

    private void OnChanged(object? sender, DiagramChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);
}
