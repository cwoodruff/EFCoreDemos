namespace linq_query_vs_fromsql_perf.Chinook;

public class Track
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? AlbumId { get; set; }
    public int MediaTypeId { get; set; }
    public int? GenreId { get; set; }
    public string? Composer { get; set; }
    public int Milliseconds { get; set; }
    public int? Bytes { get; set; }
    public decimal UnitPrice { get; set; }

    public Album? Album { get; set; }
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new HashSet<InvoiceLine>();
}
