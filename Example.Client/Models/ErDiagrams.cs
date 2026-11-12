namespace Example.Client.Models;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

public sealed record ColumnData(string Id, string Name, string Type, bool Key);

public sealed record TableData(IReadOnlyList<ColumnData> Columns);

[JsonSerializable(typeof(TableData))]
public sealed partial class ErJsonContext : JsonSerializerContext;

public static class ErDiagrams
{
    public const double HeaderHeight = 40;

    public const double RowHeight = 20;

    public const double Width = 220;

    public static IReadOnlyList<string> Types { get; } = ["int", "text", "date", "decimal", "bool"];

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "table",
                DisplayName = "テーブル",
                Size = new DiagramSize(Width, HeaderHeight + RowHeight),
                RowHeaderHeight = HeaderHeight,
                RowHeight = RowHeight,
                Permissions = DiagramPermissions.All & ~DiagramPermissions.Resize,
                Look = new DiagramLook { Fill = "var(--example-node-fill)", Stroke = "var(--example-node-stroke)" },
                Slots = [new DiagramLabelSlot { Name = "name", MaxLines = 1 }],
                DataType = ErJsonContext.Default.TableData,
                DefaultData = NewTable(),
                GetPorts = static box => box.Data is TableData table ? Ports(table) : [],
                CreateContent = static _ => new DiagramContent { Label = "new_table", Data = NewTable() }
            }
        ],
        LinkTypes = [new DiagramLinkType { Name = "fk", Look = new DiagramLook { Stroke = "var(--example-flow-line)" } }],
        DefaultLinkType = "fk"
    };

    public static double HeightOf(int columns) => HeaderHeight + (columns * RowHeight);

    public static IReadOnlyList<ColumnData> ColumnsOf(object? data) => data is TableData table ? table.Columns : [];

    public static string NextId(IEnumerable<ColumnData> columns) =>
        string.Create(CultureInfo.InvariantCulture, $"c{columns.Select(static x => int.TryParse(x.Id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty().Max() + 1}");

    public static Diagram Create()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Table("order_items", 40, 140, ("id", "int", true), ("order_id", "int", false), ("product_id", "int", false), ("quantity", "int", false)));
        diagram.Add(Table("orders", 360, 40, ("id", "int", true), ("customer_id", "int", false), ("ordered_at", "date", false)));
        diagram.Add(Table("products", 360, 260, ("id", "int", true), ("name", "text", false), ("price", "decimal", false)));
        diagram.Add(Table("customers", 680, 40, ("id", "int", true), ("name", "text", false), ("email", "text", false)));
        diagram.Connect(Key("items-order", "order_items", "c2", "orders"));
        diagram.Connect(Key("items-product", "order_items", "c3", "products"));
        diagram.Connect(Key("order-customer", "orders", "c2", "customers"));
        diagram.ClearHistory();
        return diagram;
    }

    private static TableData NewTable() => new([new ColumnData("c1", "id", "int", true)]);

    private static ImmutableArray<DiagramPortDefinition> Ports(TableData table) =>
        [
            .. table.Columns.SelectMany(static (column, row) => new[]
            {
                new DiagramPortDefinition { Id = $"{column.Id}:in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Row = row, Kind = column.Key ? "key" : "column", Visibility = DiagramPortVisibility.WhileConnecting, Label = column.Name },
                new DiagramPortDefinition { Id = $"{column.Id}:out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Row = row, Kind = "key", Label = column.Name }
            })
        ];

    private static DiagramNode Table(string name, double x, double y, params (string Name, string Type, bool Key)[] columns)
    {
        IReadOnlyList<ColumnData> list = [.. columns.Select(static (c, i) => new ColumnData(string.Create(CultureInfo.InvariantCulture, $"c{i + 1}"), c.Name, c.Type, c.Key))];
        return new DiagramNode { Id = name, Type = "table", Bounds = new DiagramRect(x, y, Width, HeightOf(list.Count)), Label = name, Data = new TableData(list) };
    }

    private static DiagramLink Key(string id, string source, string column, string target) =>
        new() { Id = id, Type = "fk", Source = new DiagramLinkEnd(source, $"{column}:out"), Target = new DiagramLinkEnd(target, "c1:in") };
}
