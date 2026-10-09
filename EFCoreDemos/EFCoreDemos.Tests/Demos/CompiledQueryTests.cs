extern alias compiled_query;

using compiled_query::compiled_query.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// compiled-query: EF.CompileQuery / EF.CompileAsyncQuery delegates (exposed by the demo's ChinookContext as
    /// GetAlbum, GetTracksByArtistId, ...) must return the same results as the equivalent runtime LINQ query,
    /// stay parameterized (one delegate, any argument) and still go through the change tracker.
    /// </summary>
    public class CompiledQueryTests : IDisposable
    {
        private const int AlbumCount = 347;
        private const int IronMaidenArtistId = 90;
        private const int IronMaidenAlbumCount = 21;
        private const int IronMaidenTrackCount = 213;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(347)]
        public void GetAlbum_MatchesRuntimeLinq(int id)
        {
            using var compiledDb = CreateContext();
            using var linqDb = CreateContext();

            var compiled = compiledDb.GetAlbum(id);
            var linq = linqDb.Albums.AsNoTracking().FirstOrDefault(a => a.Id == id);

            Assert.NotNull(compiled);
            Assert.NotNull(linq);
            Assert.Equal(linq.Id, compiled.Id);
            Assert.Equal(linq.Title, compiled.Title);
            Assert.Equal(linq.ArtistId, compiled.ArtistId);
        }

        [Fact]
        public void GetAlbum_MissingId_ReturnsNull_AndAlbumExistsAgrees()
        {
            using var db = CreateContext();

            Assert.Null(db.GetAlbum(-1));
            Assert.False(db.AlbumExists(-1));
            Assert.True(db.AlbumExists(1));
        }

        [Fact]
        public void CollectionCompiledQueries_MatchRuntimeLinq()
        {
            using var compiledDb = CreateContext();
            using var linqDb = CreateContext();

            var compiledAlbums = compiledDb.GetAlbumsByArtistId(IronMaidenArtistId).Select(a => a.Id).Order().ToList();
            var linqAlbums = linqDb.Albums.Where(a => a.ArtistId == IronMaidenArtistId).Select(a => a.Id).Order().ToList();
            Assert.Equal(linqAlbums, compiledAlbums);
            Assert.Equal(IronMaidenAlbumCount, compiledAlbums.Count);

            var compiledTracks = compiledDb.GetTracksByArtistId(IronMaidenArtistId).Select(t => t.Id).Order().ToList();
            var linqTracks = linqDb.Albums.Where(a => a.ArtistId == IronMaidenArtistId)
                .SelectMany(a => a.Tracks).Select(t => t.Id).Order().ToList();
            Assert.Equal(linqTracks, compiledTracks);
            Assert.Equal(IronMaidenTrackCount, compiledTracks.Count);

            Assert.Equal(AlbumCount, compiledDb.GetAllAlbums().Count());
        }

        [Fact]
        public async Task AsyncCompiledQueries_MatchSyncCompiledQueries()
        {
            using var db = CreateContext();

            var syncAlbum = db.GetAlbum(10);
            var asyncAlbum = await db.GetAlbumAsync(10);
            Assert.NotNull(asyncAlbum);
            Assert.Equal(syncAlbum!.Title, asyncAlbum.Title);

            var asyncTracks = new List<int>();
            await foreach (var track in db.GetTracksByArtistIdAsync(IronMaidenArtistId))
            {
                asyncTracks.Add(track.Id);
            }

            Assert.Equal(
                db.GetTracksByArtistId(IronMaidenArtistId).Select(t => t.Id).Order(),
                asyncTracks.Order());

            Assert.True(await db.AlbumExistsAsync(1));
        }

        [Fact]
        public void CompiledQuery_IsParameterized_OneRoundTripPerCall_SameSql()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var first = db.GetAlbum(1);
            var second = db.GetAlbum(2);

            Assert.NotEqual(first!.Title, second!.Title);
            var commands = log.Commands;
            Assert.Equal(2, commands.Count);
            Assert.Equal(SqlOf(commands[0]), SqlOf(commands[1]));
            Assert.Contains("@", SqlOf(commands[0]));
        }

        [Fact]
        public void CompiledQuery_ResultsAreTracked_AndIdentityResolved()
        {
            using var db = CreateContext();

            var viaCompiled = db.GetAlbum(5);
            var viaLinq = db.Albums.First(a => a.Id == 5);

            Assert.Same(viaCompiled, viaLinq);
            Assert.Single(db.ChangeTracker.Entries<Album>());
        }

        // The log entry contains "Executed DbCommand (..ms) [Parameters=[...], ...]" followed by the SQL on the next lines.
        private static string SqlOf(string logEntry)
        {
            var executed = logEntry.IndexOf("Executed DbCommand", StringComparison.Ordinal);
            var newline = logEntry.IndexOf('\n', Math.Max(executed, 0));
            return newline < 0 ? logEntry : logEntry[(newline + 1)..].Trim();
        }
    }
}
