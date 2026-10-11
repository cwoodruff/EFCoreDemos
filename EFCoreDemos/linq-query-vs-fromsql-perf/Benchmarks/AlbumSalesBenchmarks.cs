using BenchmarkDotNet.Attributes;
using linq_query_vs_fromsql_perf.Chinook;
using Microsoft.EntityFrameworkCore;

namespace linq_query_vs_fromsql_perf.Benchmarks;

/// <summary>
/// Compares a 4-table aggregate report (Invoice, InvoiceLine, Track, Album) written as
/// an EF Core LINQ query against the same report as a hand-written SELECT via FromSql.
/// See <see cref="AlbumSalesQueries"/> for both versions of the query.
///
/// AlbumSalesSummary is keyless, so neither path does change tracking; the difference
/// measured is LINQ translation/query-cache overhead plus whatever SQL each path sends.
/// </summary>
[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
public class AlbumSalesBenchmarks
{
    public const string ConnectionString =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;Database=Chinook;TrustServerCertificate=True;Application Name=EFCoreDemos-LinqVsFromSqlPerf;";

    public static readonly DateTime Start = new(2009, 1, 1);
    public static readonly DateTime End = new(2011, 1, 1);
    public const decimal MinRevenue = 5.00m;

    private DbContextOptions<ChinookContext> _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        // No logger factory: console logging would dominate the measurements.
        _options = new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        // Warm up the model, connection pool and both query plans, and make sure the
        // two queries really are equivalent before timing them.
        using var context = new ChinookContext(_options);
        var linq = AlbumSalesQueries.Linq(context, Start, End, MinRevenue).ToList();
        var fromSql = AlbumSalesQueries.FromSql(context, Start, End, MinRevenue).ToList();

        if (linq.Count == 0)
            throw new InvalidOperationException("The report returned no rows; check the date range and Chinook data.");

        if (!linq.Select(Key).SequenceEqual(fromSql.Select(Key)))
            throw new InvalidOperationException(
                $"Result sets differ: LINQ returned {linq.Count} rows, FromSql returned {fromSql.Count}.");
    }

    private static (int, int, int, int, decimal, DateTime, DateTime) Key(AlbumSalesSummary s) =>
        (s.AlbumId, s.InvoiceCount, s.DistinctTracksSold, s.UnitsSold, s.Revenue, s.FirstSaleDate, s.LastSaleDate);

    [Benchmark(Baseline = true, Description = "LINQ")]
    public async Task<List<AlbumSalesSummary>> Linq()
    {
        await using var context = new ChinookContext(_options);
        return await AlbumSalesQueries.Linq(context, Start, End, MinRevenue).ToListAsync();
    }

    [Benchmark(Description = "FromSql")]
    public async Task<List<AlbumSalesSummary>> FromSql()
    {
        await using var context = new ChinookContext(_options);
        return await AlbumSalesQueries.FromSql(context, Start, End, MinRevenue).ToListAsync();
    }
}
