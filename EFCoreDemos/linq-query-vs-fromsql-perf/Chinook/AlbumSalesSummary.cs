namespace linq_query_vs_fromsql_perf.Chinook;

/// <summary>
/// Keyless result shape for the album sales report. Both the LINQ query and the
/// FromSql query materialize into this type, so the only difference measured is
/// how the SQL is produced (LINQ translation vs. hand-written SQL).
/// </summary>
public class AlbumSalesSummary
{
    public int AlbumId { get; set; }
    public string AlbumTitle { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public int DistinctTracksSold { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public DateTime FirstSaleDate { get; set; }
    public DateTime LastSaleDate { get; set; }
}
