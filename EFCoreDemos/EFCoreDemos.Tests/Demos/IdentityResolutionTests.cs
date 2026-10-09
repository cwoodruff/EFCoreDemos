extern alias identity_resolution;

using EFCoreDemos.Tests.Infrastructure;
using identity_resolution::identity_resolution.Chinook;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// identity-resolution: a tracking context hands out exactly one instance per key, so attaching a second
    /// instance with the same key throws; AsNoTracking duplicates related instances while
    /// AsNoTrackingWithIdentityResolution de-duplicates them without tracking anything.
    /// </summary>
    public class IdentityResolutionTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        [Fact]
        public void TrackedQueries_ReturnTheSameInstance_EvenThoughBothHitTheDatabase()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var first = db.Albums.Single(a => a.Id == 1);
            first.Title = "Changed in memory";

            var second = db.Albums.Single(a => a.Id == 1);

            Assert.Same(first, second);
            // The second query still round-trips, but the tracked instance (and its unsaved change) wins.
            Assert.Equal(2, log.Commands.Count);
            Assert.Equal("Changed in memory", second.Title);
            Assert.Single(db.ChangeTracker.Entries<Album>());
        }

        [Fact]
        public void Update_WithSecondInstanceOfTrackedKey_Throws()
        {
            using var db = CreateContext();

            var tracked = db.Albums.Single(a => a.Id == 1);
            var duplicate = new Album { Id = 1, Title = "London Calling" };

            var ex = Assert.Throws<InvalidOperationException>(() => db.Update(duplicate));

            Assert.Contains("another instance with the same key value", ex.Message);
            // The rejected duplicate is left behind as a Detached entry; only the original is actually tracked.
            var trackedAlbum = db.ChangeTracker.Entries<Album>().Single(e => e.State != EntityState.Detached);
            Assert.Same(tracked, trackedAlbum.Entity);
            Assert.Equal(EntityState.Unchanged, trackedAlbum.State);
        }

        [Fact]
        public void AsNoTracking_MaterializesOneArtistInstancePerRow()
        {
            using var db = CreateContext();

            var albums = db.Albums.AsNoTracking().Include(a => a.Artist).OrderBy(a => a.Id).Take(5).ToList();

            Assert.Equal(5, albums.Count);
            Assert.Equal(3, albums.Select(a => a.ArtistId).Distinct().Count());
            Assert.Equal(5, albums.Select(a => a.Artist).Distinct().Count());
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public void AsNoTrackingWithIdentityResolution_DeduplicatesArtists_WithoutTracking()
        {
            using var db = CreateContext();

            var albums = db.Albums.AsNoTrackingWithIdentityResolution().Include(a => a.Artist).OrderBy(a => a.Id).Take(5).ToList();

            // Albums 1-5 belong to 3 artists (AC/DC, Accept, Aerosmith): one instance each.
            Assert.Equal(3, albums.Select(a => a.Artist).Distinct().Count());
            Assert.Same(albums[0].Artist, albums[3].Artist); // Albums 1 and 4 are both AC/DC.
            Assert.Empty(db.ChangeTracker.Entries());
        }
    }
}
