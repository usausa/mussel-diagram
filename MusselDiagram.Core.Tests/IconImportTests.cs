namespace MusselDiagram;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

using MusselDiagram.Internal;

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
    public void IconifyQuarterTurnsSwapTheSizeAndTurnAroundTheCenterOfIconify()
    {
        const string json = """
            {
              "prefix": "demo",
              "icons": { "wide": { "body": "<path d='M0 0h32v16H0z'/>", "width": 32, "height": 16 } },
              "aliases": { "tall": { "parent": "wide", "rotate": 1 }, "down": { "parent": "wide", "rotate": 2 }, "up": { "parent": "wide", "rotate": 3 } }
            }
            """;

        var icons = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json))).IconSet.Icons.ToDictionary(static x => x.Id);

        Assert.Equal((16d, 32d), (icons["tall"].Width, icons["tall"].Height));
        Assert.StartsWith("<g transform=\"rotate(90 8 8)\">", icons["tall"].Body, StringComparison.Ordinal);
        Assert.Equal((32d, 16d), (icons["down"].Width, icons["down"].Height));
        Assert.StartsWith("<g transform=\"rotate(180 16 8)\">", icons["down"].Body, StringComparison.Ordinal);
        Assert.Equal((16d, 32d), (icons["up"].Width, icons["up"].Height));
        Assert.StartsWith("<g transform=\"rotate(270 16 16)\">", icons["up"].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void IconifyTurnsAreWrittenTheSameInEveryCulture()
    {
        const string json = """{ "prefix": "demo", "icons": { "home": { "body": "<path d='M0 0'/>" } }, "aliases": { "left": { "parent": "home", "rotate": -1 } } }""";
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        DiagramIconImportResult result;
        try
        {
            result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.StartsWith("<g transform=\"rotate(270 8 8)\">", result.IconSet.Icons.Single(static x => x.Id == "left").Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAliasWithTheNameOfAnIconIsLeftOut()
    {
        const string json = """
            {
              "prefix": "demo",
              "icons": { "home": { "body": "<path d='M0 0'/>" }, "house": { "body": "<rect width='1' height='1'/>" } },
              "aliases": { "home": { "parent": "house" }, "flat": { "parent": "home", "hFlip": true }, "flat": { "parent": "house" } }
            }
            """;

        var set = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json))).IconSet;

        Assert.Equal(["home", "house", "flat"], set.Icons.Select(static x => x.Id));
        Assert.Equal("<path d=\"M0 0\"/>", set.Icons[0].Body);
        Assert.Contains("scale(-1 1)", set.Icons[2].Body, StringComparison.Ordinal);
        Assert.Equal(["demo:home", "demo:house", "demo:flat"], DiagramIconImport.CreateNodeTypes(set, new DiagramNodeType { Name = "base" }).Select(static x => x.Name));
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
            "<defs><linearGradient id=\"aws-Arch_x005F_Amazon_x002D_EC2_x005F_64-g\"><stop offset=\"0\" stop-color=\"#C8511B\"/></linearGradient></defs>" +
            "<rect width=\"80\" height=\"80\" fill=\"url(#aws-Arch_x005F_Amazon_x002D_EC2_x005F_64-g)\"/>" +
            "<path d=\"M0 0\" style=\"stroke:#fff\"/>" +
            "<use href=\"#aws-Arch_x005F_Amazon_x002D_EC2_x005F_64-g\"/>",
            icon.Body);
        Assert.Equal([DiagramIssueKind.RemovedSvgContent, DiagramIssueKind.RejectedUrl], result.Issues.Select(static x => x.Kind));
        Assert.Equal("script, onclick, foreignObject", result.Issues[0].Arguments[1]);
    }

    [Fact]
    public void ThePaintOfTheRootOfAnSvgFileStaysOnItsContent()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" id="root" x="1" y="2" width="24" height="24" viewBox="2 3 24 24" fill="none" stroke="currentColor" stroke-width="1.5" onload="alert(1)"><path d="M0 0"/></svg>""";

        var result = DiagramIconImport.FromZip(Zip(("outline.svg", svg)), "demo");

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.Equal("<g transform=\"translate(-2,-3)\"><g fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\"><path d=\"M0 0\"/></g></g>", icon.Body);
        Assert.Equal("onload", Assert.Single(result.Issues).Arguments[1]);
    }

    [Fact]
    public void TheIdsInsideIconsWithDifferentNamesDoNotMeet()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><linearGradient id="a"/><rect width="1" height="1" fill="url(#a)"/></svg>""";

        var result = DiagramIconImport.FromZip(Zip(("サーバー.svg", svg), ("ルーター.svg", svg), ("db.server.svg", svg), ("db_server.svg", svg)), "app");

        var ids = result.IconSet.Icons.Select(static x => Regex.Match(x.Body, "id=\"([^\"]+)\"").Groups[1].Value).ToList();
        Assert.Equal(4, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("app-_x30B5__x30FC__x30D0__x30FC_-a", ids);
        Assert.Contains("app-db_x002E_server-a", ids);
        Assert.Contains("app-db_x005F_server-a", ids);
    }

    [Theory]
    [InlineData("a-b", "c", "x", "a_x002D_b-c-x")]
    [InlineData("a", "b-c", "x", "a-b_x002D_c-x")]
    [InlineData("s", "a-b", "c", "s-a_x002D_b-c")]
    [InlineData("s", "a", "b-c", "s-a-b_x002D_c")]
    public void DashesInTheNamesDoNotMakeTheIdsMeet(string prefix, string icon, string id, string expected)
    {
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><linearGradient id=\"{id}\"/></svg>";

        var result = DiagramIconImport.FromZip(Zip(($"{icon}.svg", svg)), prefix);

        Assert.Equal($"<linearGradient id=\"{expected}\"/>", Assert.Single(result.IconSet.Icons).Body);
    }

    [Fact]
    public void UsesInsideAnIconThatExpandToFewElementsStay()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><defs><path id="dot" d="M0 0h1v1H0z"/><g id="pair"><use href="#dot"/><use href="#dot" x="2"/></g></defs><use href="#pair"/><use href="#pair" y="2"/></svg>""";

        var result = DiagramIconImport.FromZip(Zip(("dots.svg", svg)), "demo");

        Assert.Equal(
            "<defs><path id=\"demo-dots-dot\" d=\"M0 0h1v1H0z\"/><g id=\"demo-dots-pair\"><use href=\"#demo-dots-dot\"/><use href=\"#demo-dots-dot\" x=\"2\"/></g></defs><use href=\"#demo-dots-pair\"/><use href=\"#demo-dots-pair\" y=\"2\"/>",
            Assert.Single(result.IconSet.Icons).Body);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void UsesOfElementsWithUsesAreRemovedWhenTheyAddMoreThanTenThousandElements()
    {
        var levels = String.Concat(Enumerable.Range(1, 5).Select(static i => $"<g id=\"l{i}\">{String.Concat(Enumerable.Repeat($"<use href=\"#l{i - 1}\"/>", 10))}</g>"));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><defs><path id=\"l0\" d=\"M0 0h1v1H0z\"/>{levels}</defs><use href=\"#l5\"/></svg>";

        var result = DiagramIconImport.FromZip(Zip(("bomb.svg", svg)), "demo");

        var body = Assert.Single(result.IconSet.Icons).Body;
        Assert.Equal(Enumerable.Repeat("demo-bomb-l0", 10), Regex.Matches(body, "href=\"#([^\"]+)\"").Select(static x => x.Groups[1].Value));
        Assert.Contains("<g id=\"demo-bomb-l5\"></g>", body, StringComparison.Ordinal);
        var issue = Assert.Single(result.Issues);
        Assert.Equal((DiagramIssueKind.RemovedSvgContent, "use"), (issue.Kind, issue.Arguments[1]));
    }

    [Fact]
    public void EveryUseIsRemovedWhenTheUsesStillAddMoreThanTenThousandElementsWithoutTheNestedOnes()
    {
        var paths = String.Concat(Enumerable.Repeat("<path d=\"M0 0h1v1H0z\"/>", 2_000));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><defs><g id=\"big\">{paths}</g></defs>{String.Concat(Enumerable.Repeat("<use href=\"#big\"/>", 10))}</svg>";

        var result = DiagramIconImport.FromZip(Zip(("wide.svg", svg)), "demo");

        Assert.DoesNotContain("<use", Assert.Single(result.IconSet.Icons).Body, StringComparison.Ordinal);
        var issue = Assert.Single(result.Issues);
        Assert.Equal((DiagramIssueKind.RemovedSvgContent, "use"), (issue.Kind, issue.Arguments[1]));
    }

    [Theory]
    [InlineData(98, 98)]
    [InlineData(99, 0)]
    public void UsesAreRemovedOnlyWhenTheyAddMoreThanTenThousandElements(int uses, int kept)
    {
        var paths = String.Concat(Enumerable.Repeat("<path d=\"M0 0h1v1H0z\"/>", 100));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><defs><g id=\"g\">{paths}</g></defs>{String.Concat(Enumerable.Repeat("<use href=\"#g\"/>", uses))}</svg>";

        var result = DiagramIconImport.FromZip(Zip(("many.svg", svg)), "demo");

        Assert.Equal(kept, Regex.Count(Assert.Single(result.IconSet.Icons).Body, "<use "));
        Assert.Equal(kept == 0, result.Issues.Any(static x => x.Kind == DiagramIssueKind.RemovedSvgContent));
    }

    [Fact]
    public void AFewUsesInALargeIconStay()
    {
        var paths = String.Concat(Enumerable.Repeat("<path d=\"M0 0h1v1H0z\"/>", 11_000));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><defs><path id=\"dot\" d=\"M0 0h1v1H0z\"/></defs>{paths}<use href=\"#dot\"/><use href=\"#dot\" x=\"2\"/></svg>";

        var result = DiagramIconImport.FromZip(Zip(("large.svg", svg)), "demo");

        Assert.Equal(2, Regex.Count(Assert.Single(result.IconSet.Icons).Body, "<use "));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void LongChainsOfUsesAreCutWhenTheyAddMoreThanTenThousandElements()
    {
        var chain = String.Concat(Enumerable.Range(0, 20_000).Select(static i => $"<use id=\"u{i}\" href=\"#u{i + 1}\"/>"));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">{chain}<path id=\"u20000\" d=\"M0 0\"/></svg>";

        var result = DiagramIconImport.FromZip(Zip(("chain.svg", svg)), "demo");

        Assert.Equal(["demo-chain-u20000"], Regex.Matches(Assert.Single(result.IconSet.Icons).Body, "href=\"#([^\"]+)\"").Select(static x => x.Groups[1].Value));
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
    public void SizesBeyondDoubleAreNotUsed()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1e400 24"><rect width="1" height="1"/></svg>""";

        var icon = Assert.Single(DiagramIconImport.FromZip(Zip(("wide.svg", svg)), "demo").IconSet.Icons);

        Assert.Equal((24d, 24d), (icon.Width, icon.Height));
    }

    [Fact]
    public void AnExceptionFromTheMapOfTheAppIsNotTakenForABrokenZip()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><rect width="1" height="1"/></svg>""";

        Assert.Throws<InvalidDataException>(() => DiagramIconImport.FromZip(Zip(("icon.svg", svg)), "demo", static _ => throw new InvalidDataException("mine")));
    }

    [Fact]
    public void AliasesOfAliasesFollowTheChain()
    {
        const string json = """
            {
              "prefix": "demo",
              "icons": { "home": { "body": "<path d='M0 0'/>" } },
              "aliases": {
                "a2": { "parent": "a1", "hFlip": true },
                "a1": { "parent": "home", "rotate": 1 },
                "loop1": { "parent": "loop2" },
                "loop2": { "parent": "loop1" }
              }
            }
            """;

        var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(["home", "a2", "a1"], result.IconSet.Icons.Select(static x => x.Id));
        var chained = result.IconSet.Icons[1].Body;
        Assert.Contains("rotate(90", chained, StringComparison.Ordinal);
        Assert.Contains("scale(-1 1)", chained, StringComparison.Ordinal);
        Assert.Equal(2, result.Issues.Length);
    }

    [Fact]
    public void AnAliasOfAnAliasTakesTheNearestSizeAndOffsetAndFlipsBothWays()
    {
        const string json = """
            {
              "prefix": "demo",
              "width": 24,
              "height": 24,
              "icons": { "home": { "body": "<path d='M0 0h1v1z'/>" } },
              "aliases": {
                "outer": { "parent": "home", "width": 32, "height": 20, "left": 2, "top": 3, "vFlip": true },
                "inner": { "parent": "outer", "width": 16, "vFlip": true }
              }
            }
            """;

        var icons = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json))).IconSet.Icons;

        var outer = icons.Single(static x => x.Id == "outer");
        var inner = icons.Single(static x => x.Id == "inner");
        Assert.Equal((32d, 20d), (outer.Width, outer.Height));
        Assert.Equal((16d, 20d), (inner.Width, inner.Height));
        Assert.Contains("scale(1 -1)", outer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("scale(1 -1)", inner.Body, StringComparison.Ordinal);
        Assert.Contains("translate(-2 -3)", outer.Body, StringComparison.Ordinal);
        Assert.Contains("translate(-2 -3)", inner.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public Task ALongCycleOfAliasesEndsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var aliases = String.Join(", ", Enumerable.Range(0, 30000).Select(static i => $"\"a{i}\": {{ \"parent\": \"a{(i + 1) % 30000}\" }}"));
                var json = $$"""{ "prefix": "demo", "icons": { "home": { "body": "<path d='M0 0'/>" } }, "aliases": { {{aliases}} } }""";

                var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

                Assert.Equal(["home"], result.IconSet.Icons.Select(static x => x.Id));
                Assert.Equal(30000, result.Issues.Length);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task ALongChainOfAliasesEndsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var aliases = String.Join(", ", Enumerable.Range(0, 30000).Select(static i => $"\"a{i}\": {{ \"parent\": \"{(i == 29999 ? "home" : $"a{i + 1}")}\", \"rotate\": 1 }}"));
                var json = $$"""{ "prefix": "demo", "icons": { "home": { "body": "<path d='M0 0'/>" } }, "aliases": { {{aliases}} } }""";

                var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

                Assert.Equal(30001, result.IconSet.Icons.Length);
                Assert.Empty(result.Issues);
                Assert.DoesNotContain("rotate(", result.IconSet.Icons.Single(static x => x.Id == "a0").Body, StringComparison.Ordinal);
                Assert.Contains("rotate(90", result.IconSet.Icons.Single(static x => x.Id == "a29999").Body, StringComparison.Ordinal);
                Assert.Contains("rotate(180", result.IconSet.Icons.Single(static x => x.Id == "a29998").Body, StringComparison.Ordinal);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public Task AVeryLongChainOfAliasesEndsQuickly()
    {
        return Task.Run(
            static () =>
            {
                var aliases = String.Join(", ", Enumerable.Range(0, 1000000).Select(static i => $"\"a{i}\": {{ \"parent\": \"a{i + 1}\" }}"));
                var json = $$"""{ "prefix": "demo", "icons": { "home": { "body": "<path d='M0 0'/>" } }, "aliases": { {{aliases}} } }""";

                var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

                Assert.Equal(["home"], result.IconSet.Icons.Select(static x => x.Id));
                Assert.Equal(1000000, result.Issues.Length);
            },
            TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x41)]
    public void AnEncryptedFileIsAnUnreadableIcon(int flags)
    {
        var bytes = Zip(("locked.svg", "<svg/>"), ("open.svg", "<svg><rect width='1' height='1'/></svg>")).ToArray();
        Encrypt(bytes, [0x50, 0x4B, 0x03, 0x04], 6, flags);
        Encrypt(bytes, [0x50, 0x4B, 0x01, 0x02], 8, flags);

        var result = DiagramIconImport.FromZip(new MemoryStream(bytes), "demo");

        Assert.Equal(["open"], result.IconSet.Icons.Select(static x => x.Id));
        Assert.Equal(DiagramIssueKind.UnreadableIcon, Assert.Single(result.Issues).Kind);
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
    public void MalformedSetsAreReportedOrRejected()
    {
        const string json = """
            {
              "prefix": "demo",
              "width": 1e400,
              "icons": {
                "number": 1,
                "nobody": { "width": 10 },
                "text": { "body": 2 },
                "box": { "body": "<rect width=\"10\" height=\"10\"/>" }
              },
              "aliases": { "loose": 3, "orphan": { "parent": 4 } },
              "categories": { "Shapes": ["box", 5] }
            }
            """;

        var result = DiagramIconImport.FromIconify(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(["box"], result.IconSet.Icons.Select(static x => x.Id));
        Assert.Equal(16, result.IconSet.Icons[0].Width);
        Assert.Equal("Shapes", result.IconSet.Icons[0].Category);
        Assert.Equal(5, result.Issues.Length);
        Assert.Throws<DiagramException>(() => DiagramIconImport.FromIconify(new MemoryStream("""{ "prefix": 1, "icons": {} }"""u8.ToArray())));
        Assert.Throws<DiagramException>(() => DiagramIconImport.FromZip(new MemoryStream("not a zip"u8.ToArray()), "demo"));
    }

    [Fact(Timeout = 10_000)]
    public async Task DeeplyNestedContentIsRemoved()
    {
        const int levels = 400_000;
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">" + String.Concat(Enumerable.Repeat("<g>", levels)) + "<path d=\"M0 0\"/>" + String.Concat(Enumerable.Repeat("</g>", levels)) + "</svg>";

        var result = await Task.Run(() => DiagramIconImport.FromZip(Zip(("deep.svg", svg)), "demo"), TestContext.Current.CancellationToken);

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.Equal(256, Regex.Count(icon.Body, "<g>"));
        Assert.DoesNotContain("<path", icon.Body, StringComparison.Ordinal);
        var issue = Assert.Single(result.Issues);
        Assert.Equal((DiagramIssueKind.RemovedSvgContent, "g"), (issue.Kind, issue.Arguments[1]));
    }

    [Fact]
    public void UrlsHiddenByEscapesOrImageSetsAreRemoved()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
              <path d="M0 0" fill="u\rl(https://example.com/a.svg#p)"/>
              <path d="M0 0" stroke="\75 rl(https://example.com/b.svg#p)"/>
              <rect width="1" height="1" style="mask-image:image-set('https://example.com/c.png' 1x);stroke:#fff"/>
              <rect width="1" height="1" style="mask-image:-WEBKIT-IMAGE-SET('https://example.com/d.png' 1x)"/>
            </svg>
            """;

        var result = DiagramIconImport.FromZip(Zip(("hidden.svg", svg)), "demo");

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.DoesNotContain("example.com", icon.Body, StringComparison.Ordinal);
        Assert.Contains("style=\"stroke:#fff\"", icon.Body, StringComparison.Ordinal);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(DiagramIssueKind.RejectedUrl, issue.Kind);
        Assert.Contains("u\\rl(https://example.com/a.svg#p)", issue.Arguments[1], StringComparison.Ordinal);
        Assert.Contains("-WEBKIT-IMAGE-SET('https://example.com/d.png' 1x)", issue.Arguments[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AZipWithABrokenDirectoryIsReported()
    {
        var bytes = Zip(("a.svg", "<svg xmlns='http://www.w3.org/2000/svg'/>")).ToArray();
        var at = bytes.AsSpan().IndexOf([(byte)0x50, (byte)0x4B, (byte)0x01, (byte)0x02]);
        bytes[at + 3] = 0x03;

        Assert.Throws<DiagramException>(() => DiagramIconImport.FromZip(new MemoryStream(bytes), "demo"));
    }

    [Fact]
    public void UrlsInQuotesOrLeftOpenAreRemoved()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
              <rect width="1" height="1" style="mask-image:url('https://example.com/m;.png');stroke:#fff"/>
              <rect width="1" height="1" style="mask-image:url(https://example.com/n.png"/>
              <path d="M0 0" filter="url(https://example.com/f.svg#f"/>
            </svg>
            """;

        var result = DiagramIconImport.FromZip(Zip(("open.svg", svg)), "demo");

        var icon = Assert.Single(result.IconSet.Icons);
        Assert.DoesNotContain("example.com", icon.Body, StringComparison.Ordinal);
        Assert.Contains("style=\"stroke:#fff\"", icon.Body, StringComparison.Ordinal);
        Assert.Equal(DiagramIssueKind.RejectedUrl, Assert.Single(result.Issues).Kind);
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
    public void FoldersReportFilesThatCannotBeReadAndCountTheLimitInCharacters()
    {
        var folder = Directory.CreateTempSubdirectory("mussel-diagram-icons-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "open.svg"), Square);
            File.WriteAllText(Path.Combine(folder, "Upper.SVG"), Square);
            File.WriteAllText(Path.Combine(folder, "wide.svg"), $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"><desc>{new string('あ', 1_500_000)}</desc><rect width=\"8\" height=\"8\"/></svg>", Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "locked.svg"), Square);
            DiagramIconImportResult result;
            using (new FileStream(Path.Combine(folder, "locked.svg"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                result = DiagramIconImport.FromFolder(folder, "demo");
            }

            Assert.Equal(["Upper", "open", "wide"], result.IconSet.Icons.Select(static x => x.Id));
            var issue = Assert.Single(result.Issues);
            Assert.Equal((DiagramIssueKind.UnreadableIcon, "locked"), (issue.Kind, issue.Arguments[0]));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ListedFilesAreNotReadUntilLoaded()
    {
        var reads = new List<string>();

        var set = DiagramIconImport.FromFiles(
            ["b/two.svg", "a\\one.svg", "notes.txt", "c/one.svg"],
            (path, _) =>
            {
                reads.Add(path);
                return Task.FromResult<string?>(null);
            },
            "pack");

        Assert.Equal("pack", set.Prefix);
        Assert.Equal([("one", "a"), ("two", "b")], set.Icons.Select(static x => (x.Id, x.Category)));
        Assert.All(set.Icons, static x => Assert.Equal(string.Empty, x.Body));
        Assert.Empty(reads);
    }

    [Fact]
    public async Task LoadedFilesAreCleanedLikeImportedOnes()
    {
        var reads = new List<string>();
        var set = DiagramIconImport.FromFiles(
            ["Compute/Arch_Amazon-EC2_64.svg"],
            (path, _) =>
            {
                reads.Add(path);
                return Task.FromResult<string?>("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 48 32\"><script>alert(1)</script><linearGradient id=\"g\"/><rect fill=\"url(#g)\"/></svg>");
            },
            "aws");

        var content = await SvgSanitizer.LoadAsync(set, set.Icons[0], CancellationToken.None);

        Assert.Equal(["Compute/Arch_Amazon-EC2_64.svg"], reads);
        Assert.NotNull(content);
        Assert.Equal((48, 32), (content.Width, content.Height));
        Assert.Equal("<linearGradient id=\"aws-Arch_x005F_Amazon_x002D_EC2_x005F_64-g\"/><rect fill=\"url(#aws-Arch_x005F_Amazon_x002D_EC2_x005F_64-g)\"/>", content.Body);
    }

    [Fact]
    public async Task UnreadableLoadsGiveNoContent()
    {
        var large = $"<svg xmlns=\"http://www.w3.org/2000/svg\"><desc>{new string('x', SvgSanitizer.MaxFileLength)}</desc></svg>";
        Func<CancellationToken, Task<string?>>[] loads =
        [
            static _ => Task.FromResult<string?>(null),
            static _ => Task.FromResult<string?>("<svg><g></svg>"),
            static _ => Task.FromException<string?>(new IOException()),
            static _ => throw new IOException(),
            static _ => Task.FromCanceled<string?>(new CancellationToken(true)),
            _ => Task.FromResult<string?>(large)
        ];

        foreach (var load in loads)
        {
            var set = new DiagramIconSet { Prefix = "pack", Icons = [new DiagramIcon { Id = "icon", Load = load }] };
            Assert.Null(await SvgSanitizer.LoadAsync(set, set.Icons[0], CancellationToken.None));
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

    private static void Encrypt(byte[] bytes, byte[] signature, int offset, int flags)
    {
        var at = bytes.AsSpan().IndexOf(signature) + offset;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at)) | flags));
    }

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
