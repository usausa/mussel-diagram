namespace MusselDiagram;

public readonly record struct DiagramPoint(double X, double Y);

public readonly record struct DiagramSize(double Width, double Height);

public readonly record struct DiagramRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public DiagramPoint Center => new(X + (Width / 2), Y + (Height / 2));

    public DiagramRect Offset(DiagramPoint offset) => this with { X = X + offset.X, Y = Y + offset.Y };

    public bool Intersects(DiagramRect other) => (X <= other.Right) && (other.X <= Right) && (Y <= other.Bottom) && (other.Y <= Bottom);
}

public readonly record struct DiagramThickness(double Left, double Top, double Right, double Bottom);
