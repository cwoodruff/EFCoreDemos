extern alias filtered_include;

using System.Text.RegularExpressions;
using EFCoreDemos.Tests.Infrastructure;
using filtered_include::filtered_include.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// filtered-include: <c>Include(e => e.Albums.Where(...))</c> loads only the children that match the filter,
    /// while still returning every parent.
    /// </summary>
    public class FilteredIncludeTests : IDisposable
    {
        // Seeded Chinook: 275 artists, 347 albums; 18 album titles contain "the" (case-sensitive instr),
        // spread across 17 artists. A case-insensitive match would find 80.
        private const int ArtistCount = 275;
        private const int AlbumCount = 347;
        private const int AlbumsContainingThe = 18;
        private const int ArtistsWithMatchingAlbums = 17;

        private readonly ChinookCopy _db = new();
        private readonly SqlLog _log = new();

        public void Dispose() => _db.Dispose();

        /// <summary>The demo context only has a parameterless ctor; point it at the copy and capture SQL.</summary>
        private sealed class LoggedContext(string connectionString, SqlLog log) : ChinookContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder
                    .UseSqlite(connectionString)
                    .LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
                base.OnConfiguring(optionsBuilder);
            }
        }

        private LoggedContext CreateContext() => new(_db.ConnectionString, _log);

        private static IQueryable<Artist> DemoQuery(ChinookContext db) =>
            db.Artists.Include(e => e.Albums.Where(a => a.Title.Contains("the")));

        [Fact]
        public void FilteredInclude_LoadsOnlyMatchingAlbums()
        {
            using var db = CreateContext();

            var artists = DemoQuery(db).AsSplitQuery().ToList();

            var loadedAlbums = artists.SelectMany(a => a.Albums).ToList();
            Assert.Equal(AlbumsContainingThe, loadedAlbums.Count);
            Assert.All(loadedAlbums, album => Assert.Contains("the", album.Title, StringComparison.Ordinal));
            Assert.Equal(ArtistsWithMatchingAlbums, artists.Count(a => a.Albums.Count > 0));
        }

        [Fact]
        public void FilteredInclude_StillReturnsEveryParent()
        {
            using var db = CreateContext();

            var artists = DemoQuery(db).AsSplitQuery().ToList();

            Assert.Equal(ArtistCount, artists.Count);
            // Nothing beyond the filtered albums was tracked, i.e. the filter ran in the database.
            Assert.Equal(AlbumsContainingThe, db.ChangeTracker.Entries<Album>().Count());
        }

        [Fact]
        public void UnfilteredInclude_LoadsAllAlbums()
        {
            using var db = CreateContext();

            var artists = db.Artists.Include(e => e.Albums).AsSplitQuery().ToList();

            Assert.Equal(AlbumCount, artists.Sum(a => a.Albums.Count));
        }

        [Fact]
        public void FilteredInclude_FilterIsTranslatedToSql()
        {
            using var db = CreateContext();

            // AsSingleQuery so ToQueryString shows the whole statement (split mode only shows the first query).
            var sql = DemoQuery(db).AsSingleQuery().ToQueryString();

            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Matches(new Regex("'%?the%?'"), sql);
        }

        [Fact]
        public void FilteredInclude_AsSplitQuery_ExecutesTwoCommandsWithFilterInChildQuery()
        {
            using var db = CreateContext();

            _ = DemoQuery(db).AsSplitQuery().ToList();

            var commands = _log.Commands;
            Assert.Equal(2, commands.Count);
            Assert.Contains(commands, c => c.Contains("\"Album\"") && Regex.IsMatch(c, "'%?the%?'"));
        }
    }
}
