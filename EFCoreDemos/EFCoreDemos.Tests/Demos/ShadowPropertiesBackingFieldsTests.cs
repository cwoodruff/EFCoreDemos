extern alias shadow_properties_backing_fields;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using shadow_properties_backing_fields::shadow_properties_backing_fields.Model;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// shadow-properties-backing-fields: plain domain classes (private setters, a private _tracks field behind
    /// IReadOnlyCollection) mapped with shadow audit columns stamped in SaveChanges and ConfigureConventions
    /// defaults. Each test gets a fresh SQLite file and a controllable clock.
    /// </summary>
    public class ShadowPropertiesBackingFieldsTests : IDisposable
    {
        private static readonly DateTime Start = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        private readonly TempSqliteFile _file = new();
        private readonly SqlLog _log = new();
        private DateTime _now = Start;

        public ShadowPropertiesBackingFieldsTests()
        {
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _file.Dispose();

        private MusicContext CreateContext() => new(MusicContext.CreateOptions(_file.ConnectionString, _log.Add), () => _now);

        private int SeedAlbum(string title = "Kind of Blue", params string[] tracks)
        {
            using var db = CreateContext();
            var album = new Album(title);
            foreach (var name in tracks.Length > 0 ? tracks : ["So What", "Freddie Freeloader", "Blue in Green"])
            {
                album.AddTrack(name, 0.99m);
            }

            db.Albums.Add(album);
            db.SaveChanges();
            return album.Id;
        }

        [Fact]
        public void Model_AddsAuditColumnsAndForeignKeyAsShadowProperties()
        {
            using var db = CreateContext();
            var album = db.Model.FindEntityType(typeof(Album))!;
            var track = db.Model.FindEntityType(typeof(Track))!;

            Assert.True(album.FindProperty(MusicContext.CreatedAt)!.IsShadowProperty());
            Assert.True(album.FindProperty(MusicContext.LastUpdated)!.IsShadowProperty());
            Assert.True(track.FindProperty(MusicContext.CreatedAt)!.IsShadowProperty());
            Assert.True(track.FindProperty(MusicContext.LastUpdated)!.IsShadowProperty());
            Assert.True(track.FindProperty("AlbumId")!.IsShadowProperty());

            // ...and none of them exist on the CLR types.
            Assert.Null(typeof(Album).GetProperty(MusicContext.CreatedAt));
            Assert.Null(typeof(Track).GetProperty("AlbumId"));
        }

        [Fact]
        public void Model_MapsTracksThroughTheBackingField()
        {
            using var db = CreateContext();
            var navigation = db.Model.FindEntityType(typeof(Album))!.FindNavigation(nameof(Album.Tracks))!;

            Assert.Equal("_tracks", navigation.FieldInfo?.Name);
            Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
        }

        [Fact]
        public void ConfigureConventions_SetsDefaults_AndExplicitConfigurationWins()
        {
            using var db = CreateContext();
            var album = db.Model.FindEntityType(typeof(Album))!;
            var track = db.Model.FindEntityType(typeof(Track))!;

            Assert.Equal(200, track.FindProperty(nameof(Track.Name))!.GetMaxLength());
            Assert.Equal(10, track.FindProperty(nameof(Track.UnitPrice))!.GetPrecision());
            Assert.Equal(2, track.FindProperty(nameof(Track.UnitPrice))!.GetScale());
            Assert.Equal(160, album.FindProperty(nameof(Album.Title))!.GetMaxLength());
        }

        [Fact]
        public void DomainClasses_ExposeNoPublicSetters()
        {
            var settable = typeof(Album).GetProperties().Concat(typeof(Track).GetProperties())
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
                .ToList();

            Assert.Empty(settable);
            Assert.DoesNotContain(typeof(Album).GetConstructors(), c => c.GetParameters().Length == 0);
        }

        [Fact]
        public void Tracks_CannotBeMutatedFromOutside()
        {
            var album = new Album("Kind of Blue");
            album.AddTrack("So What", 0.99m);

            Assert.Throws<NotSupportedException>(() => ((ICollection<Track>)album.Tracks).Add(new Track("Bonus", 0m)));
            Assert.Throws<NotSupportedException>(() => ((ICollection<Track>)album.Tracks).Clear());
            Assert.Throws<InvalidOperationException>(() => album.AddTrack("so what", 1.29m));
            Assert.Single(album.Tracks);
        }

        [Fact]
        public void BackingField_IsSavedAndLoadedByEf()
        {
            var id = SeedAlbum();

            using var db = CreateContext();
            var album = db.Albums.Include(a => a.Tracks).Single(a => a.Id == id);

            Assert.Equal("Kind of Blue", album.Title);
            Assert.Equal(["Blue in Green", "Freddie Freeloader", "So What"], album.Tracks.Select(t => t.Name).Order());
            Assert.Equal(3, db.Tracks.Count(t => EF.Property<int>(t, "AlbumId") == id));
        }

        [Fact]
        public void SaveChanges_StampsBothAuditColumnsOnInsert()
        {
            var id = SeedAlbum();

            using var db = CreateContext();
            var row = db.Albums.Where(a => a.Id == id)
                .Select(a => new
                {
                    Created = EF.Property<DateTime>(a, MusicContext.CreatedAt),
                    Updated = EF.Property<DateTime>(a, MusicContext.LastUpdated)
                })
                .Single();

            Assert.Equal(Start, row.Created);
            Assert.Equal(Start, row.Updated);
            Assert.All(db.Tracks.Select(t => EF.Property<DateTime>(t, MusicContext.CreatedAt)).ToList(), c => Assert.Equal(Start, c));
        }

        [Fact]
        public void SaveChanges_OnUpdate_MovesLastUpdatedOnly_AndOnlyForModifiedEntities()
        {
            var changedId = SeedAlbum("Kind of Blue");
            var untouchedId = SeedAlbum("A Love Supreme", "Acknowledgement");
            _now = Start.AddDays(3);

            using (var db = CreateContext())
            {
                db.Albums.Single(a => a.Id == changedId).Rename("Kind of Blue (Legacy Edition)");
                db.Albums.Single(a => a.Id == untouchedId); // tracked, not modified
                db.SaveChanges();
            }

            using var check = CreateContext();
            var rows = check.Albums
                .Select(a => new
                {
                    a.Id,
                    Created = EF.Property<DateTime>(a, MusicContext.CreatedAt),
                    Updated = EF.Property<DateTime>(a, MusicContext.LastUpdated)
                })
                .ToDictionary(r => r.Id);

            Assert.Equal(Start, rows[changedId].Created);
            Assert.Equal(Start.AddDays(3), rows[changedId].Updated);
            Assert.Equal(Start, rows[untouchedId].Updated);
        }

        [Fact]
        public void EfProperty_FiltersAndOrdersOnShadowColumnsInSql()
        {
            SeedAlbum("A Love Supreme", "Acknowledgement");
            _now = Start.AddDays(3);
            SeedAlbum("Kind of Blue");

            using var db = CreateContext();
            var query = db.Albums
                .Where(a => EF.Property<DateTime>(a, MusicContext.LastUpdated) >= Start.AddDays(1))
                .OrderByDescending(a => EF.Property<DateTime>(a, MusicContext.LastUpdated))
                .Select(a => a.Title);

            var sql = query.ToQueryString();
            Assert.Contains("WHERE \"a\".\"LastUpdated\" >=", sql);
            Assert.Contains("ORDER BY \"a\".\"LastUpdated\" DESC", sql);
            Assert.Equal(["Kind of Blue"], query.ToList());
        }

        [Fact]
        public void MigrationPreview_ShowsConventionFacetsAndShadowColumns()
        {
            using var db = CreateContext();

            var code = MigrationPreview.ScaffoldInitial(db).MigrationCode;

            Assert.Contains("maxLength: 160", code);
            Assert.Contains("maxLength: 200", code);
            Assert.Contains("precision: 10, scale: 2", code);
            Assert.Contains("CreatedAt = table.Column<DateTime>", code);
            Assert.Contains("AlbumId = table.Column<int>(type: \"INTEGER\", nullable: false)", code);
        }
    }
}
