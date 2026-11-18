namespace MusselDiagram;

using System.Text.RegularExpressions;

public sealed partial class StyleSheetTests
{
    [Fact]
    public void EveryThemeVariableIsDeclaredWithoutSpecificity()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselDiagram", "wwwroot", "MusselDiagram.css"));

        var selectors = Rule().Matches(css)
            .Where(static x => Declaration().IsMatch(x.Groups["body"].Value))
            .SelectMany(static x => TopLevelComma().Split(x.Groups["selector"].Value))
            .Select(static x => x.Trim())
            .ToList();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, static x => Assert.Matches(WhereOnly(), x));
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"(?:^|;)\s*--mussel-[a-z0-9-]+\s*:")]
    private static partial Regex Declaration();

    [GeneratedRegex(@",(?![^(]*\))")]
    private static partial Regex TopLevelComma();

    [GeneratedRegex(@"^(?::where\([^()]*\)\s*)+$")]
    private static partial Regex WhereOnly();
}
