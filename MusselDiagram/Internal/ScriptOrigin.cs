namespace MusselDiagram.Internal;

using System.Text.Json;

internal sealed class ScriptOrigin
{
    private readonly Dictionary<string, (DiagramRect Bounds, string? Parent)> boxes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, (DiagramLinkEnd Source, DiagramLinkEnd Target)> links = new(StringComparer.Ordinal);

    private Dictionary<string, List<string>>? children;

    private ScriptOrigin()
    {
    }

    public string? Text { get; private set; }

    public static ScriptOrigin Read(JsonElement element)
    {
        var origin = new ScriptOrigin();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return origin;
        }

        if (element.TryGetProperty("boxes", out var boxes))
        {
            foreach (var box in boxes.EnumerateArray())
            {
                origin.boxes[box[0].GetString()!] = (new DiagramRect(box[1].GetDouble(), box[2].GetDouble(), box[3].GetDouble(), box[4].GetDouble()), box[5].GetString());
            }
        }

        if (element.TryGetProperty("links", out var links))
        {
            foreach (var link in links.EnumerateArray())
            {
                origin.links[link[0].GetString()!] = (new DiagramLinkEnd(link[1].GetString()!, link[2].GetString()), new DiagramLinkEnd(link[3].GetString()!, link[4].GetString()));
            }
        }

        if (element.TryGetProperty("text", out var text))
        {
            origin.Text = text.GetString();
        }

        return origin;
    }

    public bool TryGetBox(string id, out DiagramRect bounds, out string? parent)
    {
        var found = boxes.TryGetValue(id, out var box);
        (bounds, parent) = box;
        return found;
    }

    public bool TryGetLink(string id, out DiagramLinkEnd source, out DiagramLinkEnd target)
    {
        var found = links.TryGetValue(id, out var link);
        (source, target) = link;
        return found;
    }

    public int CountDescendants(string id)
    {
        var map = children ??= ChildrenMap();
        var seen = new HashSet<string>(StringComparer.Ordinal) { id };
        var stack = new Stack<string>();
        stack.Push(id);
        while (stack.Count > 0)
        {
            if (!map.TryGetValue(stack.Pop(), out var list))
            {
                continue;
            }

            foreach (var child in list)
            {
                if (seen.Add(child))
                {
                    stack.Push(child);
                }
            }
        }

        return seen.Count - 1;
    }

    private Dictionary<string, List<string>> ChildrenMap()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (child, box) in boxes)
        {
            if (box.Parent is not { } parent)
            {
                continue;
            }

            if (!map.TryGetValue(parent, out var list))
            {
                list = [];
                map[parent] = list;
            }

            list.Add(child);
        }

        return map;
    }
}
