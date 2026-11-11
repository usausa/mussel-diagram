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

    public string? DefaultLinkType { get; init; }

    public string? DefaultGroupType { get; init; }

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

    public ImmutableArray<string>? AllowedParents { get; init; }

    public bool TopLevel { get; init; } = true;

    public bool StayInParent { get; init; }

    public int? MaxCount { get; init; }

    public bool Required { get; init; }

    public bool Undeletable { get; init; }

    public bool Start { get; init; }
}

public sealed record DiagramNodeType : DiagramBoxType;

public sealed record DiagramGroupType : DiagramBoxType
{
    public DiagramGroupKind Kind { get; init; }

    public DiagramGroupSizing Sizing { get; init; }

    public DiagramThickness Padding { get; init; } = new(20, 40, 20, 20);

    public ImmutableArray<string>? AllowedChildren { get; init; }

    public ImmutableArray<string>? AllowedMembers { get; init; }

    public bool FillHitTest { get; init; }
}

public sealed record DiagramLinkType : DiagramType
{
    public DiagramLook Look { get; init; } = new();

    public DiagramLineForm Form { get; init; }

    public double StubLength { get; init; } = 20;

    public double ObstacleMargin { get; init; } = 10;

    public DiagramMarker SourceMarker { get; init; }

    public DiagramMarker TargetMarker { get; init; } = DiagramMarker.Arrow;

    public bool AllowSelf { get; init; }

    public bool AllowDuplicate { get; init; }

    public bool AllowCrossParent { get; init; } = true;

    public bool AllowCycles { get; init; } = true;
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
}

public sealed record DiagramLabelSlot
{
    public required string Name { get; init; }

    public DiagramLabelPlacement Placement { get; init; } = DiagramLabelPlacement.Center;

    public DiagramTextFormat? Format { get; init; }

    public double? Width { get; init; }

    public int? MaxLines { get; init; }

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

public sealed record DiagramLinkGeometry(ImmutableArray<DiagramPoint> Points, DiagramPoint SourcePoint, DiagramPoint TargetPoint, string Path);

public sealed record DiagramIconSet
{
    public required string Prefix { get; init; }

    public ImmutableArray<DiagramIcon> Icons { get; init; } = [];
}

public sealed record DiagramIcon
{
    public required string Id { get; init; }

    public required string Body { get; init; }

    public double Width { get; init; } = 24;

    public double Height { get; init; } = 24;

    public string? Name { get; init; }
}
