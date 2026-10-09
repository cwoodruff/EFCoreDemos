extern alias split_queries;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using split_queries::split_queries.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// split-queries: AsSplitQuery loads a collection Include with one SQL command per collection, while
    /// AsSingleQuery uses one JOINed command. Both shapes must return the same graph.
    /// </summary>
    public class SplitQueriesTests : IDisposable
    {
        // Seeded Chinook counts.
        private const int ArtistCount = 275;
        private const int AlbumCount = 347;
        private const int InvoiceCount = 458;
        private const int InvoiceLineCount = 2662;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog log) => new(_db.Options<ChinookContext>(log));

        [Fact]
        public void AsSplitQuery_IssuesOneCommandPerCollection()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var artists = db.Artists.Include(e => e.Albums).AsSplitQuery().ToList();

            Assert.Equal(2, log.Commands.Count);
            Assert.Equal(ArtistCount, artists.Count);
            Assert.Equal(AlbumCount, artists.Sum(a => a.Albums.Count));
        }

        [Fact]
        public void AsSingleQuery_IssuesOneJoinedCommand()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var invoices = db.Invoices.Include(e => e.InvoiceLines).AsSingleQuery().ToList();

            var command = Assert.Single(log.Commands);
            Assert.Contains("JOIN", command, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(InvoiceCount, invoices.Count);
            Assert.Equal(InvoiceLineCount, invoices.Sum(i => i.InvoiceLines.Count));
        }

        [Fact]
        public void SplitAndSingle_ReturnIdenticalArtistGraphs()
        {
            var splitLog = new SqlLog();
            var singleLog = new SqlLog();
            using var splitDb = CreateContext(splitLog);
            using var singleDb = CreateContext(singleLog);

            var split = Shape(splitDb.Artists.Include(e => e.Albums).AsSplitQuery().ToList());
            var single = Shape(singleDb.Artists.Include(e => e.Albums).AsSingleQuery().ToList());

            Assert.Equal(single, split);
            Assert.Equal(2, splitLog.Commands.Count);
            Assert.Single(singleLog.Commands);
        }

        [Fact]
        public void SplitAndSingle_ReturnIdenticalInvoiceGraphs()
        {
            using var splitDb = CreateContext(new SqlLog());
            using var singleDb = CreateContext(new SqlLog());

            var split = splitDb.Invoices.Include(e => e.InvoiceLines).AsSplitQuery().ToList()
                .OrderBy(i => i.Id)
                .Select(i => (i.Id, Lines: string.Join(",", i.InvoiceLines.Select(l => l.Id).OrderBy(x => x))))
                .ToList();
            var single = singleDb.Invoices.Include(e => e.InvoiceLines).AsSingleQuery().ToList()
                .OrderBy(i => i.Id)
                .Select(i => (i.Id, Lines: string.Join(",", i.InvoiceLines.Select(l => l.Id).OrderBy(x => x))))
                .ToList();

            Assert.Equal(single, split);
        }

        [Fact]
        public void AsSplitQuery_FirstCommandLoadsArtistsOnly_SecondLoadsAlbums()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            _ = db.Artists.Include(e => e.Albums).AsSplitQuery().ToList();

            var commands = log.Commands;
            Assert.Equal(2, commands.Count);
            Assert.DoesNotContain("\"Album\"", commands[0]);
            Assert.Contains("\"Album\"", commands[1]);
        }

        private static List<(int Id, string Albums)> Shape(List<Artist> artists) =>
            artists
                .OrderBy(a => a.Id)
                .Select(a => (a.Id, string.Join(",", a.Albums.Select(al => al.Id).OrderBy(x => x))))
                .ToList();
    }
}
