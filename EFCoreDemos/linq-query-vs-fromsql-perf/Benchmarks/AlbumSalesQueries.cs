using System.Data;
using linq_query_vs_fromsql_perf.Chinook;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace linq_query_vs_fromsql_perf.Benchmarks;

/// <summary>
/// The album sales report, written twice: once as LINQ and once as a hand-written
/// SQL SELECT executed through FromSql. Both return the same rows in the same order.
///
/// For every album sold in [start, end): how many invoices it appeared on, how many of
/// its tracks sold, total units, total revenue and first/last sale date - keeping only
/// albums that earned at least minRevenue, highest revenue first.
/// </summary>
public static class AlbumSalesQueries
{
    public static IQueryable<AlbumSalesSummary> Linq(
        ChinookContext context, DateTime start, DateTime end, decimal minRevenue) =>
        (from i in context.Invoices
         join il in context.InvoiceLines on i.Id equals il.InvoiceId
         join t in context.Tracks on il.TrackId equals t.Id
         join a in context.Albums on t.AlbumId equals a.Id
         where i.InvoiceDate >= start && i.InvoiceDate < end
         group new { InvoiceId = i.Id, i.InvoiceDate, TrackId = t.Id, il.Quantity, il.UnitPrice }
             by new { AlbumId = a.Id, AlbumTitle = a.Title }
         into g
         select new AlbumSalesSummary
         {
             AlbumId = g.Key.AlbumId,
             AlbumTitle = g.Key.AlbumTitle,
             InvoiceCount = g.Select(x => x.InvoiceId).Distinct().Count(),
             DistinctTracksSold = g.Select(x => x.TrackId).Distinct().Count(),
             UnitsSold = g.Sum(x => x.Quantity),
             Revenue = g.Sum(x => x.UnitPrice * x.Quantity),
             FirstSaleDate = g.Min(x => x.InvoiceDate),
             LastSaleDate = g.Max(x => x.InvoiceDate)
         })
        .Where(s => s.Revenue >= minRevenue)
        .OrderByDescending(s => s.Revenue)
        .ThenBy(s => s.AlbumTitle)
        .ThenBy(s => s.AlbumId);

    // Interpolated values become DbParameters (@p0, @p1, @p2) - this is not string concatenation.
    //
    // A plain DateTime would be sent as datetime2, but Invoice.InvoiceDate is datetime. SQL Server
    // then converts the column to datetime2 on every row (datetime2 has higher type precedence),
    // which can turn an index seek into a scan. Passing SqlParameters typed as DateTime matches the
    // column exactly - the same thing LINQ does automatically from the model's HasColumnType("datetime").
    public static IQueryable<AlbumSalesSummary> FromSql(
        ChinookContext context, DateTime start, DateTime end, decimal minRevenue)
    {
        var startParam = new SqlParameter { SqlDbType = SqlDbType.DateTime, Value = start };
        var endParam = new SqlParameter { SqlDbType = SqlDbType.DateTime, Value = end };

        return context.AlbumSalesSummaries.FromSql(
            $"""
             SELECT
                 a.Id                            AS AlbumId,
                 a.Title                         AS AlbumTitle,
                 COUNT(DISTINCT i.Id)            AS InvoiceCount,
                 COUNT(DISTINCT t.Id)            AS DistinctTracksSold,
                 SUM(il.Quantity)                AS UnitsSold,
                 SUM(il.UnitPrice * il.Quantity) AS Revenue,
                 MIN(i.InvoiceDate)              AS FirstSaleDate,
                 MAX(i.InvoiceDate)              AS LastSaleDate
             FROM dbo.Invoice AS i
             INNER JOIN dbo.InvoiceLine AS il ON il.InvoiceId = i.Id
             INNER JOIN dbo.Track       AS t  ON t.Id = il.TrackId
             INNER JOIN dbo.Album       AS a  ON a.Id = t.AlbumId
             WHERE i.InvoiceDate >= {startParam}
               AND i.InvoiceDate <  {endParam}
             GROUP BY a.Id, a.Title
             HAVING SUM(il.UnitPrice * il.Quantity) >= {minRevenue}
             ORDER BY Revenue DESC, AlbumTitle, AlbumId
             """);
    }
}
