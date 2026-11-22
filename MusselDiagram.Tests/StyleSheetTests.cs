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

    [Fact]
    public void TheStatesOfReconnectingWaitingAndEditingSlotsStayInsideTheDiagram()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselDiagram", "wwwroot", "MusselDiagram.css"));

        var selectors = Rule().Matches(css)
            .SelectMany(static x => TopLevelComma().Split(x.Groups["selector"].Value))
            .Select(static x => x.Trim())
            .Where(static x => x.Contains("[data-reconnecting]", StringComparison.Ordinal) || x.Contains("[data-pending]", StringComparison.Ordinal) || x.Contains("[data-slot][data-editing]", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, selectors.Count);
        Assert.All(selectors, static x => Assert.StartsWith(":where(.mussel-diagram) ", x, StringComparison.Ordinal));
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
