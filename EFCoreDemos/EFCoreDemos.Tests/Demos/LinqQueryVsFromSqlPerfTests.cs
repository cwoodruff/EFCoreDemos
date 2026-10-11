extern alias linq_query_vs_fromsql_perf;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using linq_query_vs_fromsql_perf::linq_query_vs_fromsql_perf.Benchmarks;
using linq_query_vs_fromsql_perf::linq_query_vs_fromsql_perf.Chinook;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// linq-query-vs-fromsql-perf: benchmarks a 4-table album sales report (Invoice, InvoiceLine, Track,
    /// Album) written in LINQ vs the same report as a hand-written SELECT via FromSql. The comparison is
    /// only fair if both return the same rows; these tests check that without running BenchmarkDotNet.
    /// Read-only against Chinook.
    /// </summary>
    public class LinqQueryVsFromSqlPerfTests
    {
        private static ChinookContext CreateContext() => new(
            new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;")
                .Options);

        private static List<(int, string, int, int, int, decimal, DateTime, DateTime)> Normalize(
            IEnumerable<AlbumSalesSummary> rows) =>
            rows.Select(r => (r.AlbumId, r.AlbumTitle, r.InvoiceCount, r.DistinctTracksSold, r.UnitsSold,
                r.Revenue, r.FirstSaleDate, r.LastSaleDate)).ToList();

        [Fact]
        public void LinqAndFromSqlPaths_GenerateTheExpectedSql()
        {
            using var db = CreateContext();

            var linqSql = AlbumSalesQueries.Linq(db, AlbumSalesBenchmarks.Start, AlbumSalesBenchmarks.End,
                AlbumSalesBenchmarks.MinRevenue).ToQueryString();
            var fromSql = AlbumSalesQueries.FromSql(db, AlbumSalesBenchmarks.Start, AlbumSalesBenchmarks.End,
                AlbumSalesBenchmarks.MinRevenue).ToQueryString();

            // LINQ should translate the whole report server-side: joins, grouping, distinct counts, HAVING.
            Assert.Contains("INNER JOIN [InvoiceLine]", linqSql);
            Assert.Contains("INNER JOIN [Album]", linqSql);
            Assert.Contains("COUNT(DISTINCT", linqSql);
            Assert.Contains("GROUP BY", linqSql);
            Assert.Contains("HAVING", linqSql);

            Assert.Contains("FROM dbo.Invoice AS i", fromSql);
            Assert.Contains("HAVING SUM(il.UnitPrice * il.Quantity) >= @p2", fromSql);
        }

        [SqlServerFact]
        public async Task Benchmarks_LinqAndFromSqlReturnIdenticalRowsInTheSameOrder()
        {
            var benchmarks = new AlbumSalesBenchmarks();
            benchmarks.Setup(); // throws if the two result sets differ

            var linq = await benchmarks.Linq();
            var fromSql = await benchmarks.FromSql();

            Assert.NotEmpty(linq);
            Assert.Equal(Normalize(linq), Normalize(fromSql));
        }

        [SqlServerFact]
        public async Task Report_RespectsRevenueThresholdAndDateRange()
        {
            await using var db = CreateContext();

            var rows = await AlbumSalesQueries.FromSql(db, AlbumSalesBenchmarks.Start, AlbumSalesBenchmarks.End,
                AlbumSalesBenchmarks.MinRevenue).ToListAsync();

            Assert.NotEmpty(rows);
            Assert.All(rows, r =>
            {
                Assert.True(r.Revenue >= AlbumSalesBenchmarks.MinRevenue);
                Assert.True(r.FirstSaleDate >= AlbumSalesBenchmarks.Start);
                Assert.True(r.LastSaleDate < AlbumSalesBenchmarks.End);
                Assert.True(r.DistinctTracksSold <= r.UnitsSold);
            });
            Assert.Equal(rows.OrderByDescending(r => r.Revenue).Select(r => r.Revenue), rows.Select(r => r.Revenue));
        }
    }
}
