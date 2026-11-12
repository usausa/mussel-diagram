namespace MusselDiagram;

using System.Collections.Immutable;

public enum DiagramExportBackground
{
    Transparent,
    Theme
}

public enum DiagramImageFormat
{
    Png,
    Jpeg
}

public sealed record DiagramSvgExportOptions
{
    public double Margin { get; init; } = 10;

    public DiagramExportBackground Background { get; init; }

    public bool IncludeOverlays { get; init; }

    public bool EmbedFonts { get; init; }
}

public sealed record DiagramImageExportOptions
{
    public double Margin { get; init; } = 10;

    public DiagramExportBackground Background { get; init; }

    public bool IncludeOverlays { get; init; }

    public DiagramImageFormat Format { get; init; }

    public double Scale { get; init; } = 1;

    public double Quality { get; init; } = 0.92;
}

public sealed class DiagramExportResult
{
    internal DiagramExportResult(Stream? content, DiagramReason? error, ImmutableArray<DiagramIssue> issues)
    {
        Content = content;
        Error = error;
        Issues = issues;
    }

    public Stream? Content { get; }

    public DiagramReason? Error { get; }

    public ImmutableArray<DiagramIssue> Issues { get; }
}
