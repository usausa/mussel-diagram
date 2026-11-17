namespace MusselDiagram;

using MusselDiagram.Languages.Japanese;

public sealed class LanguageTests
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes = [new DiagramNodeType { Name = "server", Ports = [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left }] }],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }]
    };

    [Fact]
    public void MessagesFollowTheUserInterfaceCulture()
    {
        var options = new MusselDiagramOptions();
        var english = CreateDiagram().Validate().Single();
        var japanese = InCulture(Japanese, static () => CreateDiagram().Validate().Single());

        Assert.Equal("The type 'legacy' is not registered.", english.Message);
        Assert.Equal("型「legacy」は登録されていません", japanese.Message);
        Assert.Equal(english.Arguments, japanese.Arguments);
        Assert.Equal("ここにはつなぐ所がありません", InCulture(Japanese, () => options.Reason(DiagramReasonKind.NoConnectionPoint)));
        Assert.Equal("最前面へ移動", InCulture(Japanese, () => options.Command("BringToFront")));
        Assert.Equal("前面へ移動", InCulture(Japanese, () => options.Command("BringForward")));
        Assert.Equal("Mine", InCulture(Japanese, () => options.Command("Mine")));
    }

    [Fact]
    public void EachTextIsSearchedFromTheCultureToItsParents()
    {
        var options = new MusselDiagramOptions
        {
            Cultures =
            {
                [Japanese.Name] = new DiagramTexts
                {
                    Reasons = new Dictionary<DiagramReasonKind, string> { [DiagramReasonKind.Cycle] = "循環はできません", [DiagramReasonKind.Limit] = string.Empty },
                    NoMatches = string.Empty
                }
            }
        };

        Assert.Equal("循環はできません", InCulture(Japanese, () => options.Reason(DiagramReasonKind.Cycle)));
        Assert.Equal(JapaneseTexts.Default.Reasons[DiagramReasonKind.Limit], InCulture(Japanese, () => options.Reason(DiagramReasonKind.Limit)));
        Assert.Equal(JapaneseTexts.Default.NoMatches, InCulture(Japanese, () => options.NoMatches));
        Assert.Equal(DiagramTexts.English.Reasons[DiagramReasonKind.Cycle], options.Reason(DiagramReasonKind.Cycle));
    }

    [Fact]
    public void LanguagesWithoutATextFallBackToEnglish()
    {
        var options = new MusselDiagramOptions { Cultures = { ["fr"] = new DiagramTexts { SearchPlaceholder = "Rechercher" } } };
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Equal("Rechercher", InCulture(culture, () => options.SearchPlaceholder));
        Assert.Equal("No matches", InCulture(culture, () => options.NoMatches));
        Assert.Equal("The overlay group has no members.", InCulture(culture, () => options.Issue(DiagramIssueKind.NoMembers, [])));
    }

    [Fact]
    public void TheOptionsOfADiagramChooseItsTexts()
    {
        var options = new MusselDiagramOptions
        {
            Cultures =
            {
                [string.Empty] = new DiagramTexts
                {
                    Issues = new Dictionary<DiagramIssueKind, string> { [DiagramIssueKind.UnregisteredType] = "No type {0}", [DiagramIssueKind.InvalidParent] = "Parent fixed" }
                }
            }
        };
        const string json = """{ "format": "mussel-diagram", "version": 1, "nodes": [{ "id": "a", "type": "server", "x": 0, "y": 0, "width": 40, "height": 40, "parent": "nowhere" }] }""";

        var own = CreateDiagram(options);
        var loaded = new Diagram(Schema) { Options = options };

        Assert.Equal("No type legacy", own.Validate().Single().Message);
        Assert.Equal("Parent fixed", loaded.LoadJson(json).Single().Message);
        Assert.Equal("The type 'legacy' is not registered.", CreateDiagram().Validate().Single().Message);
        Assert.Equal("The type 'legacy' is not registered.", own.Snapshot.Validate(Schema).Single().Message);
    }

    [Fact]
    public void BrokenTextsFallBackToEnglish()
    {
        var options = new MusselDiagramOptions
        {
            Cultures =
            {
                [string.Empty] = new DiagramTexts { Issues = new Dictionary<DiagramIssueKind, string> { [DiagramIssueKind.UnregisteredType] = "No type {0} {1" } }
            }
        };

        Assert.Equal("The type 'legacy' is not registered.", CreateDiagram(options).Validate().Single().Message);
    }

    [Fact]
    public void EveryKindHasEnglishAndJapaneseTexts()
    {
        foreach (var kind in Enum.GetValues<DiagramReasonKind>().Where(static x => x != DiagramReasonKind.App))
        {
            Assert.True(DiagramTexts.English.Reasons.ContainsKey(kind), kind.ToString());
            Assert.True(JapaneseTexts.Default.Reasons.ContainsKey(kind), kind.ToString());
        }

        foreach (var kind in Enum.GetValues<DiagramIssueKind>().Where(static x => x != DiagramIssueKind.App))
        {
            Assert.True(DiagramTexts.English.Issues.ContainsKey(kind), kind.ToString());
            Assert.True(JapaneseTexts.Default.Issues.ContainsKey(kind), kind.ToString());
        }

        Assert.Equal(DiagramTexts.English.Commands.Keys.Order(StringComparer.Ordinal), JapaneseTexts.Default.Commands.Keys.Order(StringComparer.Ordinal));
    }

    private static Diagram CreateDiagram(MusselDiagramOptions? options = null)
    {
        var diagram = new Diagram(Schema) { Options = options };
        diagram.Add(new DiagramNode { Id = "old", Type = "legacy", Bounds = new DiagramRect(0, 0, 40, 40) });
        return diagram;
    }

    private static T InCulture<T>(CultureInfo culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
