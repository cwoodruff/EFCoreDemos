extern alias keyless_entity_types;

using EFCoreDemos.Tests.Infrastructure;
using keyless_entity_types::keylessentitytypes.Chinook;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// keyless-entity-types: AlbumWithArtistName is mapped with HasNoKey().ToView(...). It can be queried
    /// like any DbSet but is never change-tracked and cannot be inserted/updated.
    /// The demo's default context targets the SQL Server Chinook view; the SQLite tests create an
    /// equivalent view on a private Chinook copy.
    /// </summary>
    public class KeylessEntityTypesTests
    {
        private const string CreateView =
            "CREATE VIEW AlbumWithArtistName AS " +
            "SELECT a.Id AS Id, a.Title AS Title, a.ArtistId AS ArtistId, ar.Name AS Name " +
            "FROM Album a JOIN Artist ar ON ar.Id = a.ArtistId;";

        [Fact]
        public void Model_AlbumWithArtistName_IsKeylessAndMappedToAView()
        {
            var options = new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;")
                .Options;
            using var db = new ChinookContext(options);

            var keyless = db.Model.FindEntityType(typeof(AlbumWithArtistName))!;
            Assert.Null(keyless.FindPrimaryKey());
            Assert.Empty(keyless.GetKeys());
            Assert.Equal("AlbumWithArtistName", keyless.GetViewName());
            Assert.Null(keyless.GetTableName());

            // Regular entities still have keys.
            Assert.NotNull(db.Model.FindEntityType(typeof(Album))!.FindPrimaryKey());
        }

        [Fact]
        public void KeylessEntity_CannotBeTracked()
        {
            using var copy = new ChinookCopy();
            using var db = new ChinookContext(copy.Options<ChinookContext>());

            var row = new AlbumWithArtistName { Id = 1, Title = "t", ArtistId = 1, Name = "n" };

            Assert.Throws<InvalidOperationException>(() => db.AlbumWithArtistName.Add(row));
            Assert.Throws<InvalidOperationException>(() => db.Attach(row));
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public void KeylessQuery_ReturnsViewRows_WithoutTrackingThem()
        {
            using var copy = new ChinookCopy();
            using var db = new ChinookContext(copy.Options<ChinookContext>());
            db.Database.ExecuteSqlRaw(CreateView);

            Assert.Equal(QueryTrackingBehavior.TrackAll, db.ChangeTracker.QueryTrackingBehavior);

            var rows = db.AlbumWithArtistName.ToList();

            Assert.Equal(347, rows.Count);
            Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Name)));
            Assert.Contains(rows, r => r.Name == "AC/DC" && r.Title == "For Those About To Rock We Salute You");

            // Tracking is on, yet nothing was tracked: keyless types are always read-only.
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public void KeylessQuery_ComposesWithLinq()
        {
            using var copy = new ChinookCopy();
            using var db = new ChinookContext(copy.Options<ChinookContext>());
            db.Database.ExecuteSqlRaw(CreateView);

            var acdc = db.AlbumWithArtistName
                .Where(a => a.Name == "AC/DC")
                .OrderBy(a => a.Title)
                .Select(a => a.Title)
                .ToList();

            Assert.Equal(["For Those About To Rock We Salute You", "Let There Be Rock"], acdc);
            Assert.Equal(db.Albums.Count(), db.AlbumWithArtistName.Count());
        }

        [SqlServerFact]
        public void SqlServer_KeylessView_IsQueryableAndNotTracked()
        {
            using var db = new ChinookContext();
            Assert.True(db.Database.IsSqlServer());

            var rows = db.AlbumWithArtistName.ToList();

            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Title)));
            Assert.Empty(db.ChangeTracker.Entries());
        }
    }
}
