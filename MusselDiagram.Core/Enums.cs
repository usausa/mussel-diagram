namespace MusselDiagram;

public enum DiagramGroupKind
{
    Tree,
    Overlay
}

public enum DiagramRouteState
{
    Provisional,
    Routed,
    Failed
}

[Flags]
public enum DiagramPermissions
{
    None = 0,
    Select = 1,
    Move = 2,
    Resize = 4,
    Delete = 8,
    Connect = 16,
    EditText = 32,
    All = Select | Move | Resize | Delete | Connect | EditText
}

public enum DiagramChangeOrigin
{
    User,
    Api,
    Undo,
    Redo,
    Load
}

[Flags]
public enum DiagramChangeParts
{
    None = 0,
    Position = 1,
    Size = 2,
    Ends = 4,
    Parent = 8,
    Members = 16,
    Label = 32,
    Data = 64,
    Order = 128,
    Route = 256
}

public enum DiagramLinkDirection
{
    In,
    Out,
    Both
}

public enum DiagramShape
{
    Rectangle,
    Ellipse,
    Diamond,
    Path
}

public enum DiagramDash
{
    Solid,
    Dashed,
    Dotted
}

public enum DiagramPortDirection
{
    In,
    Out,
    Both
}

public enum DiagramSide
{
    Top,
    Right,
    Bottom,
    Left
}

public enum DiagramMarker
{
    None,
    Arrow,
    OpenArrow,
    Circle,
    Diamond
}

public enum DiagramLineForm
{
    Orthogonal,
    Straight,
    Curved
}

public enum DiagramLabelPlacement
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
    Above,
    Below,
    Before,
    After,
    Header
}

public enum DiagramTextAlignment
{
    Start,
    Center,
    End
}

public enum DiagramGroupSizing
{
    Manual,
    Content
}

public enum DiagramPortVisibility
{
    Always,
    WhileConnecting
}

public enum DiagramHistoryMode
{
    Record,
    Skip,
    Merge
}
