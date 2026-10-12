namespace bulk_insert_perf.Data;

/// <summary>One imported order line. IDENTITY key, so EF has to read every new Id back after the insert.</summary>
public class OrderLine
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime OrderedAt { get; set; }

    /// <summary>Deterministic test data: the same <paramref name="count"/> rows on every run.</summary>
    public static List<OrderLine> Generate(int count)
    {
        var start = new DateTime(2026, 1, 1);
        var rows = new List<OrderLine>(count);
        for (var i = 0; i < count; i++)
        {
            rows.Add(new OrderLine
            {
                OrderNumber = $"SO-{i / 4 + 1:D6}",
                Sku = $"SKU-{i % 250:D4}",
                Quantity = i % 9 + 1,
                UnitPrice = 0.99m + i % 20,
                OrderedAt = start.AddMinutes(i)
            });
        }

        return rows;
    }
}
