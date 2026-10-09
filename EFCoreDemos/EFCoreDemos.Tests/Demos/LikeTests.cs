extern alias like;

using EFCoreDemos.Tests.Infrastructure;
using like::Demos;
using like::Demos.Chinook;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// like: EF.Functions.Like is translated to a SQL LIKE predicate (with % / _ wildcards) and runs in the database.
    /// </summary>
    public class LikeTests : IDisposable
    {
        // Seeded Chinook: 23 artist names match '%the %' (SQLite LIKE is case-insensitive for ASCII);
        // only 6 contain the exact lowercase text "the ".
        private const int ArtistsMatchingThe = 23;
        private const int ArtistsContainingLowercaseThe = 6;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        [Fact]
        public void SearchBlogs_ReturnsArtistsMatchingTheLikePattern()
        {
            using var db = CreateContext();

            var artists = new ArtistService(db).SearchBlogs("the ").ToList();

            Assert.Equal(ArtistsMatchingThe, artists.Count);
            Assert.All(artists, a => Assert.Contains("the ", a.Name, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(artists, a => a.Name == "The Rolling Stones");
            Assert.Contains(artists, a => a.Name == "Temple of the Dog");
        }

        [Fact]
        public void SearchBlogs_IsTranslatedToSqlLike()
        {
            using var db = CreateContext();

            var query = Assert.IsAssignableFrom<IQueryable<Artist>>(new ArtistService(db).SearchBlogs("the "));
            var sql = query.ToQueryString();

            Assert.Contains(" LIKE ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%the %", sql);
        }

        [Fact]
        public void Like_IsCaseInsensitiveOnSqlite_UnlikeOrdinalContains()
        {
            using var db = CreateContext();

            var likeCount = new ArtistService(db).SearchBlogs("the ").Count();
            var exactCaseCount = db.Artists.AsEnumerable().Count(a => a.Name != null && a.Name.Contains("the ", StringComparison.Ordinal));

            Assert.Equal(ArtistsMatchingThe, likeCount);
            Assert.Equal(ArtistsContainingLowercaseThe, exactCaseCount);
        }

        [Fact]
        public void Like_UnderscoreMatchesExactlyOneCharacter()
        {
            using var db = CreateContext();

            var names = db.Artists
                .Where(a => EF.Functions.Like(a.Name, "The Wh_"))
                .Select(a => a.Name)
                .ToList();

            Assert.Equal("The Who", Assert.Single(names));
        }

        [Fact]
        public void SearchBlogs_NoMatch_ReturnsEmpty()
        {
            using var db = CreateContext();

            Assert.Empty(new ArtistService(db).SearchBlogs("zzz-no-such-artist-zzz").ToList());
        }
    }
}
