extern alias projections_perf;

using System.Collections;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using projections_perf::projections_perf;
using projections_perf::projections_perf.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// projections-perf: projecting with Select (anonymous type or DTO) only reads the columns that are needed,
    /// while returning the same data as full entity hydration.
    /// </summary>
    public class ProjectionsPerfTests : IDisposable
    {
        // Every one of the 3503 seeded tracks has UnitPrice >= 0.99 and a valid album.
        private const int TrackCount = 3503;

        // Track columns that a projection of Id/Name/AlbumId (or Album.Title) must not read.
        private static readonly string[] UnneededTrackColumns =
            ["\"Composer\"", "\"Milliseconds\"", "\"Bytes\"", "\"MediaTypeId\"", "\"GenreId\"", "\"UnitPrice\""];

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        /// <summary>The part of a single SELECT statement between SELECT and FROM.</summary>
        private static string SelectClause(string sql)
        {
            var start = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            var end = sql.IndexOf("FROM", start, StringComparison.OrdinalIgnoreCase);
            Assert.True(start >= 0 && end > start, $"Unexpected SQL shape: {sql}");
            return sql[start..end];
        }

        [Fact]
        public void FullHydration_SelectsEveryTrackColumn()
        {
            using var db = CreateContext();

            var select = SelectClause(db.Tracks.Where(t => t.UnitPrice >= 0.99m).ToQueryString());

            Assert.All(UnneededTrackColumns, column => Assert.Contains(column, select));
        }

        [Fact]
        public void AnonymousProjection_SelectsOnlyProjectedColumns()
        {
            using var db = CreateContext();

            var sql = db.Tracks
                .Where(t => t.UnitPrice >= 0.99m)
                .Select(t => new { t.Id, t.Name, t.AlbumId })
                .ToQueryString();
            var select = SelectClause(sql);

            Assert.Contains("\"Id\"", select);
            Assert.Contains("\"Name\"", select);
            Assert.Contains("\"AlbumId\"", select);
            Assert.All(UnneededTrackColumns, column => Assert.DoesNotContain(column, select));
        }

        [Fact]
        public void DtoProjection_JoinsAlbumAndSelectsOnlyTitle()
        {
            using var db = CreateContext();

            var sql = db.Tracks
                .Where(t => t.UnitPrice >= 0.99m)
                .Select(t => new TrackListItem
                {
                    Id = t.Id,
                    Name = t.Name,
                    AlbumTitle = t.Album.Title
                })
                .ToQueryString();
            var select = SelectClause(sql);

            Assert.Contains("JOIN \"Album\"", sql);
            Assert.Contains("\"Title\"", select);
            Assert.DoesNotContain("\"ArtistId\"", select);
            Assert.All(UnneededTrackColumns, column => Assert.DoesNotContain(column, select));
        }

        [Fact]
        public void Projections_ReturnSameDataAsFullHydration()
        {
            using var db = CreateContext();

            var full = db.Tracks.AsNoTracking().Where(t => t.UnitPrice >= 0.99m).OrderBy(t => t.Id).ToList();
            var anon = db.Tracks
                .Where(t => t.UnitPrice >= 0.99m)
                .OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.Name, t.AlbumId })
                .ToList();
            var dto = db.Tracks
                .Where(t => t.UnitPrice >= 0.99m)
                .OrderBy(t => t.Id)
                .Select(t => new TrackListItem { Id = t.Id, Name = t.Name, AlbumTitle = t.Album.Title })
                .ToList();
            var albumTitles = db.Albums.AsNoTracking().ToDictionary(a => a.Id, a => a.Title);

            Assert.Equal(TrackCount, full.Count);
            Assert.Equal(full.Select(t => (t.Id, t.Name, t.AlbumId)), anon.Select(a => (a.Id, a.Name, a.AlbumId)));
            Assert.Equal(full.Select(t => (t.Id, t.Name, albumTitles[t.AlbumId])), dto.Select(d => (d.Id, d.Name, d.AlbumTitle)));
        }

        [Fact]
        public void Projections_DoNotTrackEntities()
        {
            using var db = CreateContext();

            _ = db.Tracks.Where(t => t.UnitPrice >= 0.99m)
                .Select(t => new TrackListItem { Id = t.Id, Name = t.Name, AlbumTitle = t.Album.Title })
                .ToList();
            Assert.Empty(db.ChangeTracker.Entries());

            _ = db.Tracks.Where(t => t.UnitPrice >= 0.99m).ToList();
            Assert.Equal(TrackCount, db.ChangeTracker.Entries<Track>().Count());
        }

        [Fact]
        public void BenchmarkMethods_ReturnTheSameRowCount()
        {
            // The benchmark methods read the shared output-folder chinook.db (read-only queries).
            var benchmarks = new ProjectionBenchmarks();

            var full = benchmarks.FullHydration();
            var anon = Assert.IsAssignableFrom<IList>(benchmarks.AnonProjection());
            var dto = benchmarks.DtoProjection();

            Assert.Equal(TrackCount, full.Count);
            Assert.Equal(TrackCount, anon.Count);
            Assert.Equal(full.Select(t => t.Id).Order(), dto.Select(d => d.Id).Order());
            Assert.All(dto, d => Assert.False(string.IsNullOrEmpty(d.AlbumTitle)));
        }
    }
}
