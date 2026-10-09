extern alias as_no_tracking_perf;

using as_no_tracking_perf::as_no_tracking_perf.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// as-no-tracking-perf: AsNoTracking skips the change tracker entirely, which is faster for reads but
    /// turns a write path into a silent bug: edits to a no-tracking entity are never saved.
    /// </summary>
    public class AsNoTrackingPerfTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        [Fact]
        public void TrackedQuery_PutsEveryEntityInTheChangeTracker()
        {
            using var db = CreateContext();

            var tracks = db.Tracks.ToList();

            Assert.NotEmpty(tracks);
            Assert.Equal(tracks.Count, db.ChangeTracker.Entries<Track>().Count());
        }

        [Fact]
        public void AsNoTrackingQuery_LeavesTheChangeTrackerEmpty()
        {
            using var db = CreateContext();

            var tracks = db.Tracks.AsNoTracking().ToList();

            Assert.NotEmpty(tracks);
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public void SilentBug_EditingNoTrackingEntity_SavesNothing()
        {
            var log = new SqlLog();
            using (var db = CreateContext(log))
            {
                var album = db.Albums.AsNoTracking().First();
                var originalTitle = album.Title;
                album.Title = originalTitle + " (edited)";

                log.Clear();
                var affected = db.SaveChanges();

                Assert.Equal(0, affected);
                Assert.False(db.ChangeTracker.HasChanges());
                Assert.Empty(log.Commands); // No UPDATE was even attempted.

                using var verify = CreateContext();
                Assert.Equal(originalTitle, verify.Albums.AsNoTracking().Single(a => a.Id == album.Id).Title);
            }
        }

        [Fact]
        public void Fix_UpdateBeforeSaveChanges_PersistsTheEdit()
        {
            int albumId;
            string newTitle;
            using (var db = CreateContext())
            {
                var album = db.Albums.AsNoTracking().First();
                albumId = album.Id;
                newTitle = album.Title + " (edited)";
                album.Title = newTitle;

                db.Update(album);
                Assert.Equal(1, db.SaveChanges());
            }

            using var verify = CreateContext();
            Assert.Equal(newTitle, verify.Albums.AsNoTracking().Single(a => a.Id == albumId).Title);
        }
    }
}
