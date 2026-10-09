extern alias linq_groupby;

using EFCoreDemos.Tests.Infrastructure;
using linq_groupby::linq_groupby.Chinook;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// linq-groupby: GroupBy + aggregate Select translates to a single SQL GROUP BY statement whose
    /// Sum/Min/Max/Average match the same aggregation done in memory.
    /// </summary>
    public class LinqGroupByTests : IDisposable
    {
        // Seeded Chinook: 458 invoices, totalling 2799.38. Each invoice has its own InvoiceDate.
        private const int InvoiceCount = 458;
        private const decimal GrandTotal = 2799.38m;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        private sealed record DateTotals(DateTime InvoiceDate, decimal Sum, decimal Min, decimal Max, decimal Avg);

        /// <summary>The demo's query, projected into a named type so results can be compared.</summary>
        private static IQueryable<DateTotals> DemoQuery(ChinookContext db) =>
            db.Invoices
                .GroupBy(o => new { o.InvoiceDate })
                .Select(g => new DateTotals(
                    g.Key.InvoiceDate,
                    g.Sum(o => o.Total),
                    g.Min(o => o.Total),
                    g.Max(o => o.Total),
                    g.Average(o => o.Total)));

        [Fact]
        public void GroupBy_TranslatesToSqlGroupBy()
        {
            using var db = CreateContext();

            var sql = DemoQuery(db).ToQueryString();

            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"InvoiceDate\"", sql);
        }

        [Fact]
        public void GroupBy_RunsAsSingleServerSideCommand()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var groups = DemoQuery(db).ToList();

            var command = Assert.Single(log.Commands);
            Assert.Contains("GROUP BY", command, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(groups);
            // Aggregation happened in the database: no Invoice entities were materialized or tracked.
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public void GroupBy_ReturnsOneRowPerDistinctInvoiceDate()
        {
            using var db = CreateContext();

            var groups = DemoQuery(db).ToList();
            var distinctDates = db.Database
                .SqlQueryRaw<int>("SELECT COUNT(DISTINCT InvoiceDate) AS Value FROM Invoice")
                .ToList()
                .Single();

            Assert.Equal(distinctDates, groups.Count);
            Assert.Equal(groups.Count, groups.Select(g => g.InvoiceDate).Distinct().Count());
        }

        [Fact]
        public void GroupBy_AggregatesMatchInMemoryGrouping()
        {
            using var db = CreateContext();

            var server = DemoQuery(db).ToList().OrderBy(g => g.InvoiceDate).ToList();
            var client = db.Invoices.AsNoTracking().ToList()
                .GroupBy(o => o.InvoiceDate)
                .Select(g => new DateTotals(
                    g.Key,
                    g.Sum(o => o.Total),
                    g.Min(o => o.Total),
                    g.Max(o => o.Total),
                    g.Average(o => o.Total)))
                .OrderBy(g => g.InvoiceDate)
                .ToList();

            Assert.Equal(client.Count, server.Count);
            for (var i = 0; i < client.Count; i++)
            {
                Assert.Equal(client[i].InvoiceDate, server[i].InvoiceDate);
                Assert.Equal(client[i].Sum, server[i].Sum);
                Assert.Equal(client[i].Min, server[i].Min);
                Assert.Equal(client[i].Max, server[i].Max);
                Assert.Equal(client[i].Avg, server[i].Avg, 6);
            }
        }

        [Fact]
        public void GroupBy_GroupSumsAddUpToInvoiceTotal()
        {
            using var db = CreateContext();

            var groups = DemoQuery(db).ToList();

            Assert.Equal(GrandTotal, groups.Sum(g => g.Sum));
            Assert.All(groups, g =>
            {
                Assert.True(g.Min <= g.Avg && g.Avg <= g.Max, $"{g.InvoiceDate}: Min/Avg/Max out of order.");
                Assert.True(g.Sum >= g.Max);
            });
            Assert.Equal(InvoiceCount, db.Invoices.Count());
        }
    }
}
