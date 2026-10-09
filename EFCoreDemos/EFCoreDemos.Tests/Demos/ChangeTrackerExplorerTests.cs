extern alias change_tracker_explorer;

using change_tracker_explorer::change_tracker_explorer.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// change-tracker-explorer: walks an Album+Tracks graph through
    /// Unchanged -> Modified -> Unchanged -> Added -> Unchanged inside a transaction that is rolled back.
    /// </summary>
    public class ChangeTrackerExplorerTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => _db.Redirect(new ChinookContext());

        private static Album LoadAlbumWithTracks(ChinookContext db) =>
            db.Albums.Include(a => a.Tracks).OrderBy(a => a.Id).First(a => a.Tracks.Any());

        private static Track NewBonusTrack(int albumId) => new()
        {
            Name = "Bonus Track",
            AlbumId = albumId,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "Demo",
            Milliseconds = 200_000,
            Bytes = 1_000_000,
            UnitPrice = 0.99m
        };

        [Fact]
        public void AfterLoad_AlbumAndTracksAreUnchanged()
        {
            using var db = CreateContext();

            var album = LoadAlbumWithTracks(db);

            Assert.NotEmpty(album.Tracks);
            Assert.Equal(EntityState.Unchanged, db.Entry(album).State);
            Assert.Equal(1 + album.Tracks.Count, db.ChangeTracker.Entries().Count());
            Assert.All(db.ChangeTracker.Entries(), e => Assert.Equal(EntityState.Unchanged, e.State));
        }

        [Fact]
        public void MutatingTitle_MarksOnlyAlbumAndTitleModified()
        {
            using var db = CreateContext();
            var album = LoadAlbumWithTracks(db);

            album.Title += " (renamed)";
            db.ChangeTracker.DetectChanges();

            var entry = db.Entry(album);
            Assert.Equal(EntityState.Modified, entry.State);
            Assert.True(entry.Property(a => a.Title).IsModified);
            Assert.False(entry.Property(a => a.ArtistId).IsModified);
            Assert.All(db.ChangeTracker.Entries<Track>(), e => Assert.Equal(EntityState.Unchanged, e.State));
        }

        [Fact]
        public void FullWalk_StatesTransitionAsTheDemoPrints()
        {
            using var db = CreateContext();
            using var tx = db.Database.BeginTransaction();

            var album = LoadAlbumWithTracks(db);
            Assert.Equal(EntityState.Unchanged, db.Entry(album).State);

            album.Title += " (renamed)";
            Assert.Equal(EntityState.Modified, db.Entry(album).State);

            db.SaveChanges();
            Assert.Equal(EntityState.Unchanged, db.Entry(album).State);

            var newTrack = NewBonusTrack(album.Id);
            db.Tracks.Add(newTrack);
            Assert.Equal(EntityState.Added, db.Entry(newTrack).State);
            Assert.Single(db.ChangeTracker.Entries(), e => e.State == EntityState.Added);

            db.SaveChanges();
            Assert.Equal(EntityState.Unchanged, db.Entry(newTrack).State);
            Assert.True(newTrack.Id > 0);
            Assert.Contains(newTrack, album.Tracks); // Fixup added it to the tracked album's collection.
            Assert.All(db.ChangeTracker.Entries(), e => Assert.Equal(EntityState.Unchanged, e.State));

            tx.Rollback();
        }

        [Fact]
        public void Rollback_LeavesDatabaseUnchanged()
        {
            int albumId;
            string originalTitle;
            int originalTrackCount;

            using (var db = CreateContext())
            using (var tx = db.Database.BeginTransaction())
            {
                var album = LoadAlbumWithTracks(db);
                albumId = album.Id;
                originalTitle = album.Title;
                originalTrackCount = album.Tracks.Count;

                album.Title += " (renamed)";
                db.SaveChanges();
                db.Tracks.Add(NewBonusTrack(album.Id));
                db.SaveChanges();

                tx.Rollback();
            }

            using var verify = CreateContext();
            var reloaded = verify.Albums.AsNoTracking().Include(a => a.Tracks).Single(a => a.Id == albumId);
            Assert.Equal(originalTitle, reloaded.Title);
            Assert.Equal(originalTrackCount, reloaded.Tracks.Count);
            Assert.DoesNotContain(reloaded.Tracks, t => t.Name == "Bonus Track");
        }
    }
}
