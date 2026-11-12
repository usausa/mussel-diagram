namespace MusselDiagram;

using System.IO.Compression;
using System.Text;

public sealed class IconImportTests
{
    [Fact]
    public void IconifySetsGiveIconsWithSizesCategoriesAndAliases()
    {
        const string json = """
            {
              "prefix": "demo",
              "width": 24,
              "height": 24,
              "icons": {
                "box": { "body": "<rect width=\"10\" height=\"10\"/>" },
                "wide": { "body": "<path d=\"M0 0h32v16H0z\"/>", "width": 32, "height": 16 }
              },
              "aliases": { "square": { "parent": "box", "hFlip": true } },
              "categories": { "Shapes": ["box", "wide"] }
            }
            """;

        var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("demo", result.IconSet.Prefix);
        Assert.Empty(result.Issues);
        var icons = result.IconSet.Icons.ToDictionary(static x => x.Id);
        Assert.Equal("<rect width=\"10\" height=\"10\"/>", icons["box"].Body);
        Assert.Equal((32, 16), (icons["wide"].Width, icons["wide"].Height));
        Assert.Equal("Shapes", icons["box"].Category);
        Assert.Equal("<g transform=\"translate(24 0) scale(-1 1)\"><rect width=\"10\" height=\"10\"/></g>", icons["square"].Body);
    }

    [Fact]
    public void ImportedSvgKeepsOnlySafeContent()
    {
        const string svg = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd">
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 80 80">
              <title>EC2</title>
              <script>alert(1)</script>
              <defs><linearGradient id="g"><stop offset="0" stop-color="#C8511B"/></linearGradient></defs>
              <rect width="80" height="80" fill="url(#g)" onclick="alert(1)"/>
              <path d="M0 0" style="fill:url(https://example.com/x.svg#a);stroke:#fff"/>
              <use xlink:href="#g"/>
              <foreignObject><div>x</div></foreignObject>
            </svg>
            """;

        var result = DiagramIconImport.FromZip(Zip(("Compute/Arch_Amazon-EC2_64.svg", svg)), "aws");

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.Equal("Arch_Amazon-EC2_64", icon.Id);
        Assert.Equal("Compute", icon.Category);
        Assert.Equal((80, 80), (icon.Width, icon.Height));
        Assert.Equal(
            "<defs><linearGradient id=\"aws-Arch_Amazon-EC2_64-g\"><stop offset=\"0\" stop-color=\"#C8511B\"/></linearGradient></defs>" +
            "<rect width=\"80\" height=\"80\" fill=\"url(#aws-Arch_Amazon-EC2_64-g)\"/>" +
            "<path d=\"M0 0\" style=\"stroke:#fff\"/>" +
            "<use href=\"#aws-Arch_Amazon-EC2_64-g\"/>",
            icon.Body);
        Assert.Equal([DiagramIssueKind.RemovedSvgContent, DiagramIssueKind.RejectedUrl], result.Issues.Select(static x => x.Kind));
        Assert.Equal("script, onclick, foreignObject", result.Issues[0].Arguments[1]);
    }

    [Fact]
    public void TheMapNamesAndSkipsFiles()
    {
        var zip = Zip(("Arch_Compute/64/Arch_Amazon-EC2_64.svg", Square), ("Arch_Compute/48/Arch_Amazon-EC2_48.svg", Square), ("readme.txt", "x"));

        var result = DiagramIconImport.FromZip(zip, "aws", static path => path.Contains("/64/", StringComparison.Ordinal) ? new DiagramIconInfo("Arch_Amazon-EC2", "Amazon EC2", "Compute") : null);

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.Equal(("Arch_Amazon-EC2", "Amazon EC2", "Compute"), (icon.Id, icon.Name, icon.Category));
    }

    [Fact]
    public void UnreadableFilesAreReported()
    {
        var result = DiagramIconImport.FromZip(Zip(("broken.svg", "<svg><g></svg>"), ("entity.svg", "<!DOCTYPE svg [<!ENTITY x \"y\">]><svg>&x;</svg>")), "demo");

        Assert.Empty(result.IconSet.Icons);
        Assert.Equal(2, result.Issues.Length);
        Assert.Throws<DiagramException>(() => DiagramIconImport.FromIconify(new MemoryStream("{}"u8.ToArray())));
    }

    [Fact]
    public void FilesOverTheSizeLimitAreReported()
    {
        var result = DiagramIconImport.FromZip(Zip(("big.svg", "<svg>" + new string(' ', 4 * 1024 * 1024) + "</svg>")), "demo");

        Assert.Empty(result.IconSet.Icons);
        Assert.Equal(DiagramIssueKind.UnreadableIcon, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public void FoldersAreReadWithTheMap()
    {
        var folder = Directory.CreateTempSubdirectory("mussel-diagram-icons-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "Arch_Compute", "64"));
            Directory.CreateDirectory(Path.Combine(folder, "Arch_Compute", "48"));
            File.WriteAllText(Path.Combine(folder, "Arch_Compute", "64", "Arch_Amazon-EC2_64.svg"), Square);
            File.WriteAllText(Path.Combine(folder, "Arch_Compute", "48", "Arch_Amazon-EC2_48.svg"), "<svg><g></svg>");

            var result = DiagramIconImport.FromFolder(folder, "aws", static path => path.Contains("/64/", StringComparison.Ordinal) ? new DiagramIconInfo("Arch_Amazon-EC2", "Amazon EC2", "Compute") : null);

            var icon = Assert.Single(result.IconSet.Icons);
            Assert.Equal(("Arch_Amazon-EC2", "Amazon EC2", "Compute"), (icon.Id, icon.Name, icon.Category));
            Assert.Empty(result.Issues);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TypesAreMadeForEachIcon()
    {
        var set = new DiagramIconSet { Prefix = "aws", Icons = [new DiagramIcon { Id = "Arch_Amazon-EC2", Body = "<path/>", Name = "Amazon EC2", Category = "Compute" }] };

        var type = Assert.Single(DiagramIconImport.CreateNodeTypes(set, new DiagramNodeType { Name = "base", Size = new DiagramSize(80, 80) }));

        Assert.Equal(("aws:Arch_Amazon-EC2", "Amazon EC2", "Compute", "aws:Arch_Amazon-EC2"), (type.Name, type.DisplayName, type.Category, type.Look.Icon));
        Assert.Equal(new DiagramSize(80, 80), type.Size);
    }

    private const string Square = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"64\"><rect width=\"64\" height=\"64\"/></svg>";

    private static MemoryStream Zip(params (string Path, string Text)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(text);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
