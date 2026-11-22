namespace MusselDiagram;

using System.Text.RegularExpressions;

public sealed partial class RenderOrderTests
{
    [Fact]
    public void AttributesAreNumberedInTheOrderTheyAreWritten()
    {
        var found = new List<string>();
        var count = 0;
        var folder = Path.Combine(TestPaths.Root, "MusselDiagram");
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file);
            if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal))
            {
                continue;
            }

            var frames = new Stack<List<(int Sequence, string Name)>>();
            foreach (Match call in Call().Matches(File.ReadAllText(file)))
            {
                switch (call.Groups["call"].Value)
                {
                    case "OpenElement" or "OpenComponent":
                        frames.Push([]);
                        break;
                    case "CloseElement" or "CloseComponent":
                        frames.TryPop(out _);
                        break;
                    default:
                        if (!call.Groups["sequence"].Success)
                        {
                            found.Add($"{relative}: {call.Value} has no number");
                            break;
                        }

                        var sequence = Int32.Parse(call.Groups["sequence"].Value, CultureInfo.InvariantCulture);
                        var name = call.Groups["name"].Value;
                        if (!frames.TryPeek(out var attributes))
                        {
                            found.Add($"{relative}: {name} ({sequence}) is outside of an element");
                            break;
                        }

                        if ((attributes.Count > 0) && (sequence <= attributes[^1].Sequence) && !attributes.Contains((sequence, name)))
                        {
                            found.Add($"{relative}: {name} ({sequence}) comes after {attributes[^1].Sequence}");
                        }

                        attributes.Add((sequence, name));
                        count++;
                        break;
                }
            }
        }

        Assert.NotEqual(0, count);
        Assert.Empty(found);
    }

    [GeneratedRegex("""builder\.(?<call>OpenElement|OpenComponent|CloseElement|CloseComponent|AddAttribute|AddComponentParameter)(?:<[^>(]*>)?\((?:(?<sequence>\d+),\s*(?<name>"[^"]*"|nameof\([^)]*\)))?""")]
    private static partial Regex Call();
}
