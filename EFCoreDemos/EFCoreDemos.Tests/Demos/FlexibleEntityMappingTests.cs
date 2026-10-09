extern alias flexible_entity_mapping;

using EFCoreDemos.Tests.Infrastructure;
using flexible_entity_mapping::flexible_entity_mapping.Chinook;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// flexible-entity-mapping: the Chinook model maps the PlaylistTrack join table as a shared-type
    /// property-bag entity (Dictionary&lt;string, object&gt;) behind the Playlist.Tracks / Track.Playlists
    /// skip navigations, and entities inherit their key from an unmapped BaseEntity class.
    /// </summary>
    public class FlexibleEntityMappingTests
    {
        private const string JoinName = "PlaylistTrack";

        [Fact]
        public void PlaylistTrack_IsASharedTypePropertyBagEntity()
        {
            using var copy = new ChinookCopy();
            using var db = copy.Redirect(new ChinookContext());

            var join = db.Model.FindEntityType(JoinName)!;
            Assert.NotNull(join);
            Assert.True(join.HasSharedClrType);
            Assert.True(join.IsPropertyBag);
            Assert.Equal(typeof(Dictionary<string, object>), join.ClrType);
            Assert.Equal(JoinName, join.GetTableName());
            Assert.Equal(["PlaylistId", "TrackId"], join.FindPrimaryKey()!.Properties.Select(p => p.Name));

            var skip = db.Model.FindEntityType(typeof(Playlist))!.FindSkipNavigation(nameof(Playlist.Tracks))!;
            Assert.Same(join, skip.JoinEntityType);
        }

        [Fact]
        public void PropertyBag_CanBeQueriedDirectlyWithIndexerProperties()
        {
            using var copy = new ChinookCopy();
            using var db = copy.Redirect(new ChinookContext());

            var joinRows = db.Set<Dictionary<string, object>>(JoinName);

            Assert.Equal(8715, joinRows.Count());
            Assert.Equal(25, joinRows.Count(r => EF.Property<int>(r, "PlaylistId") == 13));

            var row = joinRows.OrderBy(r => EF.Property<int>(r, "PlaylistId")).ThenBy(r => EF.Property<int>(r, "TrackId")).First();
            Assert.Equal(1, Convert.ToInt32(row["PlaylistId"]));
        }

        [Fact]
        public void SkipNavigation_ThroughPropertyBag_LoadsBothDirections()
        {
            using var copy = new ChinookCopy();
            using var db = copy.Redirect(new ChinookContext());

            var playlist = db.Playlists.Include(p => p.Tracks).Single(p => p.Id == 13);
            Assert.Equal(25, playlist.Tracks.Count);

            var trackId = playlist.Tracks.First().Id;
            var track = db.Tracks.Include(t => t.Playlists).Single(t => t.Id == trackId);
            Assert.Contains(track.Playlists, p => p.Id == 13);
        }

        [Fact]
        public void AddingThroughSkipNavigation_InsertsPropertyBagRow()
        {
            using var copy = new ChinookCopy();

            using (var db = copy.Redirect(new ChinookContext()))
            {
                var empty = db.Playlists.Include(p => p.Tracks).Single(p => p.Id == 2);
                Assert.Empty(empty.Tracks);

                empty.Tracks.Add(db.Tracks.Single(t => t.Id == 1));
                db.SaveChanges();
            }

            using (var db = copy.Redirect(new ChinookContext()))
            {
                var rows = db.Set<Dictionary<string, object>>(JoinName)
                    .Where(r => EF.Property<int>(r, "PlaylistId") == 2)
                    .ToList();

                var row = Assert.Single(rows);
                Assert.Equal(1, Convert.ToInt32(row["TrackId"]));
                Assert.Equal(8716, db.Set<Dictionary<string, object>>(JoinName).Count());
            }
        }

        [Fact]
        public void BaseEntity_IsAnUnmappedBaseClassProvidingTheKey()
        {
            using var copy = new ChinookCopy();
            using var db = copy.Redirect(new ChinookContext());

            Assert.Null(db.Model.FindEntityType(typeof(BaseEntity)));

            var album = db.Model.FindEntityType(typeof(Album))!;
            Assert.Null(album.BaseType);
            Assert.Equal("Album", album.GetTableName());
            Assert.Equal(nameof(BaseEntity.Id), Assert.Single(album.FindPrimaryKey()!.Properties).Name);

            Assert.Equal(347, db.Albums.Count());
        }
    }
}
