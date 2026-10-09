extern alias query_tags;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using query_tags::query_tags.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// query-tags: TagWith adds SQL comments to the generated query (visible in ToQueryString and in the
    /// executed command) without changing its results.
    /// </summary>
    public class QueryTagsTests : IDisposable
    {
        private const string DescriptionTag = "Description: Invoice Query from Query Tag demo";
        private const string LocationTag = "Query located: query_tags.Program.Main method";

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        private sealed record DateTotals(DateTime InvoiceDate, decimal Sum, decimal Min, decimal Max, decimal Avg);

        private static IQueryable<DateTotals> Untagged(ChinookContext db) =>
            db.Invoices
                .GroupBy(o => new { o.InvoiceDate })
                .Select(g => new DateTotals(
                    g.Key.InvoiceDate,
                    g.Sum(o => o.Total),
                    g.Min(o => o.Total),
                    g.Max(o => o.Total),
                    g.Average(o => o.Total)));

        /// <summary>The demo's query including its three tags.</summary>
        private static IQueryable<DateTotals> Tagged(ChinookContext db) =>
            Untagged(db)
                .TagWith(DescriptionTag)
                .TagWith(LocationTag)
                .TagWith(
                    @"Parameters:
                        None");

        [Fact]
        public void TagWith_AppearsAsSqlCommentsInToQueryString()
        {
            using var db = CreateContext();

            var sql = Tagged(db).ToQueryString();

            Assert.Contains("-- " + DescriptionTag, sql);
            Assert.Contains("-- " + LocationTag, sql);
        }

        [Fact]
        public void TagWith_MultiLineTag_EmitsACommentPerLine()
        {
            using var db = CreateContext();

            var lines = Tagged(db).ToQueryString().Split('\n').Select(l => l.TrimEnd('\r')).ToList();

            Assert.Contains(lines, l => l.StartsWith("-- Parameters:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("--", StringComparison.Ordinal) && l.Trim().EndsWith("None", StringComparison.Ordinal));
        }

        [Fact]
        public void TagWith_CommentsPrecedeTheSelectStatement()
        {
            using var db = CreateContext();

            var sql = Tagged(db).ToQueryString();

            var tagIndex = sql.IndexOf(DescriptionTag, StringComparison.Ordinal);
            var selectIndex = sql.IndexOf("SELECT", StringComparison.Ordinal);
            Assert.True(tagIndex >= 0 && tagIndex < selectIndex, sql);
        }

        [Fact]
        public void TagWith_IsSentToTheDatabaseWithTheExecutedCommand()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            _ = Tagged(db).ToList();

            var command = Assert.Single(log.Commands);
            Assert.Contains(DescriptionTag, command);
            Assert.Contains(LocationTag, command);
        }

        [Fact]
        public void TagWith_DoesNotChangeResults()
        {
            using var db = CreateContext();

            var untaggedSql = Untagged(db).ToQueryString();
            var tagged = Tagged(db).ToList().OrderBy(g => g.InvoiceDate).ToList();
            var untagged = Untagged(db).ToList().OrderBy(g => g.InvoiceDate).ToList();

            Assert.DoesNotContain("--", untaggedSql);
            Assert.NotEmpty(tagged);
            Assert.Equal(untagged, tagged);
        }
    }
}
