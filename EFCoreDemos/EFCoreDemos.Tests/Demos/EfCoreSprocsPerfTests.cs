extern alias ef_core_sprocs_perf;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ef_core_sprocs_perf::ef_core_sprocs_perf.Benchmarks;
using ef_core_sprocs_perf::sproc_mapping.Chinook;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// ef-core-sprocs-perf: benchmarks loading every Album via LINQ vs via FromSql("EXEC dbo.sproc_GetAlbum").
    /// The comparison is only fair if both paths return the same rows; these tests check that (without
    /// running BenchmarkDotNet) by calling the benchmark methods directly. Read-only against Chinook.
    /// </summary>
    public class EfCoreSprocsPerfTests
    {
        private static ChinookContext CreateContext() => new(
            new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;")
                .Options);

        private static List<(int Id, string Title, int ArtistId)> Normalize(IEnumerable<Album> albums) =>
            albums.Select(a => (a.Id, a.Title, a.ArtistId)).OrderBy(a => a.Id).ToList();

        [Fact]
        public void LinqAndSprocPaths_GenerateTheExpectedSql()
        {
            using var db = CreateContext();

            var linqSql = db.Albums.AsNoTracking().ToQueryString();
            var sprocSql = db.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking().ToQueryString();

            Assert.Contains("FROM [Album] AS [a]", linqSql);
            Assert.Contains("EXEC dbo.sproc_GetAlbum", sprocSql);
        }

        [SqlServerFact]
        public async Task NoTrackingBenchmarks_LinqAndSprocReturnIdenticalAlbums()
        {
            var benchmarks = new AlbumQueryBenchmarks();
            benchmarks.Setup(); // throws if the LINQ and sproc row counts differ

            var linq = await benchmarks.Linq_NoTracking();
            var sproc = await benchmarks.Sproc_NoTracking();

            Assert.NotEmpty(linq);
            Assert.Equal(Normalize(linq), Normalize(sproc));
        }

        [SqlServerFact]
        public async Task TrackingBenchmarks_LinqAndSprocReturnIdenticalAlbums()
        {
            var benchmarks = new AlbumQueryBenchmarks();
            benchmarks.Setup();

            var linq = await benchmarks.Linq_Tracking();
            var sproc = await benchmarks.Sproc_Tracking();

            Assert.NotEmpty(linq);
            Assert.Equal(Normalize(linq), Normalize(sproc));
        }

        [SqlServerFact]
        public async Task SprocResult_MatchesAlbumTableRowCount()
        {
            await using var db = CreateContext();

            var count = await db.Albums.CountAsync();
            var sproc = await db.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking().ToListAsync();

            Assert.Equal(count, sproc.Count);
            Assert.Equal(sproc.Count, sproc.Select(a => a.Id).Distinct().Count());
        }

        [SqlServerFact]
        public async Task TrackingVariant_TracksEntities_NoTrackingVariantDoesNot()
        {
            await using var db = CreateContext();

            await db.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking().ToListAsync();
            Assert.Empty(db.ChangeTracker.Entries());

            var tracked = await db.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").ToListAsync();
            Assert.Equal(tracked.Count, db.ChangeTracker.Entries<Album>().Count());
        }
    }
}
