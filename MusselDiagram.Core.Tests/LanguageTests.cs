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
        var english = CreateDiagram().Validate().Single();
        var japanese = InCulture(Japanese, static () => CreateDiagram().Validate().Single());

        Assert.Equal("The type 'legacy' is not registered.", english.Message);
        Assert.Equal("型「legacy」は登録されていません", japanese.Message);
        Assert.Equal(english.Arguments, japanese.Arguments);
        Assert.Equal("ここにはつなぐ所がありません", InCulture(Japanese, static () => DiagramCultures.Reason(DiagramReasonKind.NoConnectionPoint)));
        Assert.Equal("最前面へ", InCulture(Japanese, static () => DiagramCultures.Command("BringToFront")));
        Assert.Equal("Mine", InCulture(Japanese, static () => DiagramCultures.Command("Mine")));
    }

    [Fact]
    public void EachTextIsSearchedFromTheCultureToItsParents()
    {
        var culture = Japanese;
        var name = culture.Name;
        DiagramCultures.Texts[name] = new DiagramTexts
        {
            Reasons = new Dictionary<DiagramReasonKind, string> { [DiagramReasonKind.Cycle] = "循環はできません", [DiagramReasonKind.Limit] = string.Empty },
            NoMatches = string.Empty
        };
        try
        {
            Assert.Equal("循環はできません", InCulture(culture, static () => DiagramCultures.Reason(DiagramReasonKind.Cycle)));
            Assert.Equal(JapaneseLanguage.Texts.Reasons[DiagramReasonKind.Limit], InCulture(culture, static () => DiagramCultures.Reason(DiagramReasonKind.Limit)));
            Assert.Equal(JapaneseLanguage.Texts.NoMatches, InCulture(culture, static () => DiagramCultures.NoMatches));
            Assert.Equal(DiagramTexts.English.Reasons[DiagramReasonKind.Cycle], DiagramCultures.Reason(DiagramReasonKind.Cycle));
        }
        finally
        {
            DiagramCultures.Texts.Remove(name);
        }
    }

    [Fact]
    public void LanguagesWithoutATextFallBackToEnglish()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        DiagramCultures.Texts["fr"] = new DiagramTexts { SearchPlaceholder = "Rechercher" };
        try
        {
            Assert.Equal("Rechercher", InCulture(culture, static () => DiagramCultures.SearchPlaceholder));
            Assert.Equal("No matches", InCulture(culture, static () => DiagramCultures.NoMatches));
            Assert.Equal("The overlay group has no members.", InCulture(culture, static () => DiagramCultures.Issue(DiagramIssueKind.NoMembers, [])));
        }
        finally
        {
            DiagramCultures.Texts.Remove("fr");
        }
    }

    [Fact]
    public void EveryKindHasEnglishAndJapaneseTexts()
    {
        foreach (var kind in Enum.GetValues<DiagramReasonKind>().Where(static x => x != DiagramReasonKind.App))
        {
            Assert.True(DiagramTexts.English.Reasons.ContainsKey(kind), kind.ToString());
            Assert.True(JapaneseLanguage.Texts.Reasons.ContainsKey(kind), kind.ToString());
        }

        foreach (var kind in Enum.GetValues<DiagramIssueKind>().Where(static x => x != DiagramIssueKind.App))
        {
            Assert.True(DiagramTexts.English.Issues.ContainsKey(kind), kind.ToString());
            Assert.True(JapaneseLanguage.Texts.Issues.ContainsKey(kind), kind.ToString());
        }

        Assert.Equal(DiagramTexts.English.Commands.Keys.Order(StringComparer.Ordinal), JapaneseLanguage.Texts.Commands.Keys.Order(StringComparer.Ordinal));
    }

    private static Diagram CreateDiagram()
    {
        var diagram = new Diagram(Schema);
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
