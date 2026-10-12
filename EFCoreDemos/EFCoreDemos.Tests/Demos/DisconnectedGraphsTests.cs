extern alias disconnected_graphs;

using EFCoreDemos.Tests.Infrastructure;
using disconnected_graphs::disconnected_graphs;
using disconnected_graphs::disconnected_graphs.Chinook;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// disconnected-graphs: an Album + Tracks graph goes through a JSON round trip (rename one track, drop one,
    /// add one) and is saved in a new context four ways. Only load-then-diff applies all three edits; the
    /// others over-update, lose the edit, or never delete. Each test seeds its own album in a private Chinook copy.
    /// </summary>
    public class DisconnectedGraphsTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        // No LogTo: the change tracker is inspected via SavingChanges instead, so every context in this class
        // shares EF's internal service provider.
        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        private sealed record Tracked(string Type, string Name, EntityState State, IReadOnlyList<string> Modified);

        /// <summary>Runs a save and returns what the change tracker held when SaveChanges started.</summary>
        private List<Tracked> Save(Action<ChinookContext> save)
        {
            var tracked = new List<Tracked>();
            using var db = CreateContext();
            db.SavingChanges += (_, _) => tracked.AddRange(db.ChangeTracker.Entries().Select(e => new Tracked(
                e.Metadata.ClrType.Name,
                e.Entity switch { Album a => a.Title!, Track t => t.Name!, _ => "" },
                e.State,
                e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).OrderBy(n => n).ToList())));
            save(db);
            return tracked;
        }

        private (int AlbumId, Album Payload) Arrange()
        {
            int albumId;
            using (var db = CreateContext())
            {
                albumId = DemoAlbum.Seed(db);
            }

            using (var db = CreateContext())
            {
                return (albumId, Client.EditAndPut(Client.Get(db, albumId)));
            }
        }

        private List<Track> TracksOf(int albumId)
        {
            using var db = CreateContext();
            return db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).OrderBy(t => t.Id).ToList();
        }

        private static readonly string[] AllTrackColumns =
            ["AlbumId", "Bytes", "Composer", "GenreId", "MediaTypeId", "Milliseconds", "Name", "UnitPrice"];

        [Fact]
        public void ClientPayload_IsANewGraphWithTheThreeEdits()
        {
            var (albumId, payload) = Arrange();

            Assert.Equal(albumId, payload.Id);
            Assert.Equal(["Intro", DemoAlbum.NewName, "Outro", DemoAlbum.Added], payload.Tracks.Select(t => t.Name));
            Assert.Equal(0, payload.Tracks.Single(t => t.Name == DemoAlbum.Added).Id);
            Assert.DoesNotContain(payload.Tracks, t => t.Name == DemoAlbum.Removed);
        }

        [Fact]
        public void Update_RewritesEveryColumn_InsertsTheNewTrack_ButNeverDeletes()
        {
            var (albumId, payload) = Arrange();

            var tracked = Save(db => GraphSaves.Update(db, payload));

            Assert.Equal(EntityState.Modified, tracked.Single(t => t.Type == nameof(Album)).State);
            var modifiedTracks = tracked.Where(t => t.Type == nameof(Track) && t.State == EntityState.Modified).ToList();
            Assert.Equal(3, modifiedTracks.Count); // Intro and Outro didn't change, but are rewritten anyway
            Assert.All(modifiedTracks, t => Assert.Equal(AllTrackColumns, t.Modified));
            Assert.Equal(EntityState.Added, tracked.Single(t => t.Name == DemoAlbum.Added).State);

            Assert.Equal(["Intro", DemoAlbum.NewName, DemoAlbum.Removed, "Outro", DemoAlbum.Added],
                TracksOf(albumId).Select(t => t.Name));
        }

        [Fact]
        public void AttachOnly_SavesTheNewTrack_ButSilentlyLosesTheRename()
        {
            var (albumId, payload) = Arrange();

            var tracked = Save(db => GraphSaves.AttachOnly(db, payload));

            Assert.Equal(EntityState.Added, tracked.Single(t => t.Name == DemoAlbum.Added).State);
            Assert.All(tracked.Where(t => t.Name != DemoAlbum.Added), t => Assert.Equal(EntityState.Unchanged, t.State));

            Assert.Equal(["Intro", DemoAlbum.Renamed, DemoAlbum.Removed, "Outro", DemoAlbum.Added],
                TracksOf(albumId).Select(t => t.Name));
        }

        [Fact]
        public void AttachAndMark_UpdatesOnlyTheNameColumnOfTheRenamedTrack()
        {
            var (albumId, payload) = Arrange();
            var renamedId = payload.Tracks.Single(t => t.Name == DemoAlbum.NewName).Id;

            var tracked = Save(db => GraphSaves.AttachAndMark(db, payload, [renamedId]));

            var modified = Assert.Single(tracked, t => t.State == EntityState.Modified);
            Assert.Equal(DemoAlbum.NewName, modified.Name);
            Assert.Equal(["Name"], modified.Modified);
            Assert.Contains(DemoAlbum.NewName, TracksOf(albumId).Select(t => t.Name));
        }

        [Fact]
        public void TrackGraph_AppliesThePerEntityPolicy_AndStillMissesTheDeletion()
        {
            var (albumId, payload) = Arrange();
            payload.Title = "Renamed by the client"; // the policy says albums are read-only on this endpoint

            var trace = new List<string>();
            var tracked = Save(db => GraphSaves.TrackGraph(db, payload, trace.Add));

            Assert.Equal(5, trace.Count); // the album and its four payload tracks, each visited once
            Assert.Equal(EntityState.Unchanged, tracked.Single(t => t.Type == nameof(Album)).State);
            Assert.Equal(3, tracked.Count(t => t.Type == nameof(Track) && t.State == EntityState.Modified));
            Assert.Equal(EntityState.Added, tracked.Single(t => t.Name == DemoAlbum.Added).State);

            using var db = CreateContext();
            Assert.Equal(DemoAlbum.Title, db.Albums.AsNoTracking().Single(a => a.Id == albumId).Title);
            Assert.Contains(DemoAlbum.Removed, TracksOf(albumId).Select(t => t.Name));
        }

        [Fact]
        public void LoadThenDiff_AppliesExactlyTheRenameTheDeleteAndTheInsert()
        {
            var (albumId, payload) = Arrange();
            var before = TracksOf(albumId).ToDictionary(t => t.Id);

            var tracked = Save(db => GraphSaves.LoadThenDiff(db, payload));

            Assert.Equal(EntityState.Unchanged, tracked.Single(t => t.Type == nameof(Album)).State);
            var renamed = Assert.Single(tracked, t => t.State == EntityState.Modified);
            Assert.Equal(DemoAlbum.NewName, renamed.Name);
            Assert.Equal(["Name"], renamed.Modified);
            Assert.Equal(DemoAlbum.Removed, Assert.Single(tracked, t => t.State == EntityState.Deleted).Name);
            Assert.Equal(DemoAlbum.Added, Assert.Single(tracked, t => t.State == EntityState.Added).Name);

            var after = TracksOf(albumId);
            Assert.Equal(["Intro", DemoAlbum.NewName, "Outro", DemoAlbum.Added], after.Select(t => t.Name));

            // Untouched columns keep their values; the deleted row is really gone.
            var verse = after.Single(t => t.Name == DemoAlbum.NewName);
            Assert.Equal(before[verse.Id].Milliseconds, verse.Milliseconds);
            Assert.Equal(before[verse.Id].Composer, verse.Composer);
            using var db = CreateContext();
            Assert.False(db.Tracks.Any(t => t.Name == DemoAlbum.Removed && t.AlbumId == null));
        }

        [Fact]
        public void RemovingFromTheCollection_OrphansTheTrackInsteadOfDeletingIt()
        {
            int albumId;
            using (var db = CreateContext())
            {
                albumId = DemoAlbum.Seed(db);
            }

            var tracked = Save(db => GraphSaves.RemoveFromCollection(db, albumId, DemoAlbum.Removed));

            var severed = Assert.Single(tracked, t => t.State != EntityState.Unchanged);
            Assert.Equal(EntityState.Modified, severed.State);
            Assert.Equal(["AlbumId"], severed.Modified);

            Assert.DoesNotContain(DemoAlbum.Removed, TracksOf(albumId).Select(t => t.Name));
            using var check = CreateContext();
            Assert.Equal(1, check.Tracks.Count(t => t.Name == DemoAlbum.Removed && t.AlbumId == null));
        }

        [Fact]
        public void KeyValueDetection_ZeroIsAdded_AnUnknownKeyIsModifiedAndFailsTheWholeSave()
        {
            var (albumId, payload) = Arrange();
            using var db = CreateContext();

            var brandNew = new Track { Name = "Id 0", AlbumId = albumId, UnitPrice = 0.99m };
            var existing = new Track { Id = payload.Tracks[0].Id, Name = "Id set", AlbumId = albumId, UnitPrice = 0.99m };
            var ghost = new Track { Id = 999_999, Name = "No such row", AlbumId = albumId, UnitPrice = 0.99m };
            db.Tracks.UpdateRange(brandNew, existing, ghost);

            Assert.Equal(EntityState.Added, db.Entry(brandNew).State);
            Assert.Equal(EntityState.Modified, db.Entry(existing).State);
            Assert.Equal(EntityState.Modified, db.Entry(ghost).State);

            Assert.Throws<DbUpdateConcurrencyException>(() => db.SaveChanges());

            // The failed UPDATE rolls back the INSERT and the other UPDATE too.
            var tracks = TracksOf(albumId);
            Assert.DoesNotContain(tracks, t => t.Name is "Id 0" or "Id set");
            Assert.Equal(DemoAlbum.SeedTracks, tracks.Select(t => t.Name));
        }
    }
}
