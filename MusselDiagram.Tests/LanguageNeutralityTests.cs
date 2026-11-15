namespace MusselDiagram;

using System.Text;
using System.Text.RegularExpressions;

public sealed partial class LanguageNeutralityTests
{
    [Fact]
    public void OnlyTheLanguagesFolderHoldsALanguage()
    {
        var found = new List<string>();
        foreach (var project in new[] { "MusselDiagram.Core", "MusselDiagram" })
        {
            var folder = Path.Combine(TestPaths.Root, project);
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(folder, file);
                if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal) || relative.StartsWith("Languages", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Path.GetExtension(file) is ".cs" or ".razor" or ".js" or ".css" or ".csproj")
                {
                    var text = Escape().Replace(File.ReadAllText(file), static x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
                    found.AddRange(text.EnumerateRunes().Where(static x => IsLanguageLetter(x)).Select(x => $"{project}/{relative}: {x} U+{x.Value:X4}"));
                }
            }
        }

        Assert.Empty(found);
    }

    private static bool IsLanguageLetter(Rune rune) => rune.Value switch
    {
        0x3000 or (>= 0xFF00 and <= 0xFFEF) => false,
        >= 0x0E00 and <= 0x0E7F => true,
        >= 0x3000 and <= 0x30FF => true,
        >= 0x3400 and <= 0x9FFF => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        > 0x7F => Rune.IsLetter(rune),
        _ => false
    };

    [GeneratedRegex(@"\\u([0-9A-Fa-f]{4})")]
    private static partial Regex Escape();
}
