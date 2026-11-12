namespace MusselDiagram;

using System.Collections.Immutable;
using System.Text.Json.Serialization.Metadata;

public sealed record DiagramSchema
{
    public ImmutableArray<DiagramNodeType> NodeTypes { get; init; } = [];

    public ImmutableArray<DiagramGroupType> GroupTypes { get; init; } = [];

    public ImmutableArray<DiagramLinkType> LinkTypes { get; init; } = [];

    public ImmutableArray<DiagramSharedItemType> SharedItemTypes { get; init; } = [];

    public ImmutableArray<DiagramIconSet> IconSets { get; init; } = [];

    public ImmutableArray<DiagramCategory> Categories { get; init; } = [];

    public ImmutableArray<DiagramPreset> Presets { get; init; } = [];

    public string? DefaultLinkType { get; init; }

    public string? DefaultGroupType { get; init; }

    public double GridSize { get; init; } = 10;

    public IDiagramRouter? Router { get; init; }

    public DiagramAppRules Rules { get; init; } = new();
}

public abstract record DiagramType
{
    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Category { get; init; }

    public ImmutableArray<string> SearchWords { get; init; } = [];

    public JsonTypeInfo? DataType { get; init; }

    public object? DefaultData { get; init; }

    public Func<object?, IEnumerable<string>>? GetReferences { get; init; }

    public DiagramPermissions Permissions { get; init; } = DiagramPermissions.All;
}

public abstract record DiagramBoxType : DiagramType
{
    public DiagramLook Look { get; init; } = new();

    public DiagramSize Size { get; init; } = new(120, 60);

    public DiagramSize? MinSize { get; init; }

    public ImmutableArray<DiagramLabelSlot> Slots { get; init; } = [];

    public ImmutableArray<DiagramPortDefinition> Ports { get; init; } = [];

    public Func<DiagramBox, ImmutableArray<DiagramPortDefinition>>? GetPorts { get; init; }

    public bool ConnectsAsWhole { get; init; }

    public bool InPalette { get; init; } = true;

    public Func<DiagramCreateContext, DiagramContent>? CreateContent { get; init; }

    public ImmutableArray<string>? AllowedParents { get; init; }

    public bool TopLevel { get; init; } = true;

    public bool StayInParent { get; init; }

    public double RowHeaderHeight { get; init; }

    public double RowHeight { get; init; } = 20;

    public int? MaxCount { get; init; }

    public bool Required { get; init; }

    public bool Undeletable { get; init; }

    public bool Start { get; init; }

    public Func<DiagramBox, string?>? GetTooltip { get; init; }
}

public sealed record DiagramCreateContext
{
    public required DiagramSnapshot Snapshot { get; init; }

    public required DiagramBoxType Type { get; init; }

    public string? Value { get; init; }
}

public sealed record DiagramContent
{
    public string? Label { get; init; }

    public ImmutableDictionary<string, string> Texts { get; init; } = [];

    public object? Data { get; init; }
}

public sealed record DiagramCategory
{
    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    public bool Collapsed { get; init; }
}

public sealed record DiagramNodeType : DiagramBoxType;

public sealed record DiagramGroupType : DiagramBoxType
{
    public DiagramGroupKind Kind { get; init; }

    public DiagramGroupSizing Sizing { get; init; }

    public DiagramThickness Padding { get; init; } = new(20, 40, 20, 20);

    public ImmutableArray<string>? AllowedChildren { get; init; }

    public ImmutableArray<string>? AllowedMembers { get; init; }

    public DiagramGroupHeader? Header { get; init; }

    public bool FillHitTest { get; init; }
}

public sealed record DiagramGroupHeader
{
    public DiagramHeaderForm Form { get; init; }

    public double IconSize { get; init; } = 24;

    public double? NameLeft { get; init; }

    public double? NameTop { get; init; }

    public DiagramTextAlignment? NameAlignment { get; init; }
}

public sealed record DiagramLinkType : DiagramType
{
    public DiagramLook Look { get; init; } = new();

    public DiagramLineForm Form { get; init; }

    public double StubLength { get; init; } = 20;

    public double ObstacleMargin { get; init; } = 10;

    public DiagramMarker SourceMarker { get; init; }

    public DiagramMarker TargetMarker { get; init; } = DiagramMarker.Arrow;

    public IDiagramRouter? Router { get; init; }

    public bool AllowSelf { get; init; }

    public bool AllowDuplicate { get; init; }

    public bool AllowCrossParent { get; init; } = true;

    public bool AllowCycles { get; init; } = true;

    public Func<DiagramLink, string?>? GetTooltip { get; init; }
}

public sealed record DiagramSharedItemType : DiagramType
{
    public int? MaxCount { get; init; }

    public bool Required { get; init; }

    public bool Undeletable { get; init; }
}

public sealed record DiagramLook
{
    public DiagramShape Shape { get; init; }

    public string? Path { get; init; }

    public string? Icon { get; init; }

    public string? Fill { get; init; }

    public string? Stroke { get; init; }

    public double? StrokeWidth { get; init; }

    public DiagramDash Dash { get; init; }

    public string? Preset { get; init; }
}

public sealed record DiagramPreset
{
    public required string Name { get; init; }

    public required DiagramLook Look { get; init; }
}

public sealed record DiagramLabelSlot
{
    public required string Name { get; init; }

    public DiagramLabelPlacement Placement { get; init; } = DiagramLabelPlacement.Center;

    public DiagramTextFormat? Format { get; init; }

    public double? Width { get; init; }

    public int? MaxLines { get; init; }

    public bool Editable { get; init; } = true;

    public string? DefaultText { get; init; }
}

public sealed record DiagramTextFormat
{
    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public string? FontWeight { get; init; }

    public string? Color { get; init; }

    public DiagramTextAlignment? Alignment { get; init; }
}

public sealed record DiagramPortDefinition
{
    public required string Id { get; init; }

    public DiagramPortDirection Direction { get; init; } = DiagramPortDirection.Both;

    public string? Kind { get; init; }

    public DiagramSide Side { get; init; } = DiagramSide.Right;

    public double? Ratio { get; init; }

    public int? Row { get; init; }

    public double Offset { get; init; }

    public int? MaxLinks { get; init; }

    public bool Required { get; init; }

    public ImmutableArray<string>? Accepts { get; init; }

    public ImmutableArray<string>? Rejects { get; init; }

    public string? LinkType { get; init; }

    public string? Label { get; init; }

    public DiagramPortVisibility Visibility { get; init; }
}

public sealed record DiagramPort
{
    public required string Id { get; init; }

    public DiagramPortDirection Direction { get; init; }

    public string? Kind { get; init; }

    public DiagramSide Side { get; init; }

    public DiagramPoint Position { get; init; }

    public string? Label { get; init; }

    public DiagramPortVisibility Visibility { get; init; }
}

public sealed record DiagramIconSet
{
    public required string Prefix { get; init; }

    public ImmutableArray<DiagramIcon> Icons { get; init; } = [];
}

public sealed record DiagramIcon
{
    public required string Id { get; init; }

    public string Body { get; init; } = string.Empty;

    public Func<CancellationToken, Task<string?>>? Load { get; init; }

    public double Width { get; init; } = 24;

    public double Height { get; init; } = 24;

    public string? Name { get; init; }

    public string? Category { get; init; }

    public ImmutableArray<string> SearchWords { get; init; } = [];
}
