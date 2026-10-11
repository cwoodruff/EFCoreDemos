namespace linq_query_vs_fromsql_perf.Chinook;

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int TrackId { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Track Track { get; set; } = null!;
}
