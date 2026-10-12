extern alias value_converters;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using value_converters::value_converters;
using value_converters::value_converters.Model;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// value-converters: enum-as-string, strongly typed ids via ConfigureConventions, an encrypted column and a
    /// JSON list - and the trap: queries compare the STORED value, so ordering, ranges and string functions on
    /// converted columns return wrong rows or fail. Each test gets a fresh SQLite file seeded by the demo.
    /// </summary>
    public class ValueConvertersTests : IDisposable
    {
        private readonly TempSqliteFile _file = new();

        public ValueConvertersTests()
        {
            using var db = CreateContext();
            db.Database.EnsureCreated();
            CatalogSeed.Seed(db);
        }

        public void Dispose() => _file.Dispose();

        private CatalogContext CreateContext() => new(CatalogContext.CreateOptions(_file.ConnectionString));

        private string RawScalar(string sql)
        {
            using var connection = new SqliteConnection(_file.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar())!;
        }

        [Fact]
        public void Converters_ChangeWhatIsStored_ButRoundTripToTheClrValues()
        {
            Assert.Equal("Rock", RawScalar("SELECT \"Genre\" FROM \"Tracks\" WHERE \"Id\" = 1"));
            Assert.Equal("1", RawScalar("SELECT \"AlbumId\" FROM \"Tracks\" WHERE \"Id\" = 1"));
            Assert.Equal("[\"single\",\"remastered\"]", RawScalar("SELECT \"Tags\" FROM \"Tracks\" WHERE \"Id\" = 1"));

            var storedEmail = RawScalar("SELECT \"Email\" FROM \"Customers\" WHERE \"Name\" = 'Ada'");
            Assert.NotEqual("ada@example.com", storedEmail);
            Assert.Equal(EncryptedStringConverter.Encrypt("ada@example.com"), storedEmail);

            using var db = CreateContext();
            var track = db.Tracks.Single(t => t.Id == new TrackId(1));
            Assert.Equal(Genre.Rock, track.Genre);
            Assert.Equal(new AlbumId(1), track.AlbumId);
            Assert.Equal(["single", "remastered"], track.Tags);
            Assert.Equal("ada@example.com", db.Customers.Single(c => c.Name == "Ada").Email);
        }

        [Fact]
        public void ConfigureConventions_AppliesTheIdConvertersToKeysAndForeignKeys()
        {
            using var db = CreateContext();
            var track = db.Model.FindEntityType(typeof(Track))!;
            var album = db.Model.FindEntityType(typeof(Album))!;

            Assert.IsType<TrackIdConverter>(track.FindProperty(nameof(Track.Id))!.GetValueConverter());
            Assert.IsType<AlbumIdConverter>(track.FindProperty(nameof(Track.AlbumId))!.GetValueConverter());
            Assert.IsType<AlbumIdConverter>(album.FindProperty(nameof(Album.Id))!.GetValueConverter());

            var kindOfBlue = db.Albums.Include(a => a.Tracks).Single(a => a.Id == new AlbumId(2));
            Assert.Equal(["So What", "Blue in Green"], kindOfBlue.Tracks.OrderBy(t => t.Id.Value).Select(t => t.Name));
        }

        [Fact]
        public void Equality_OnConvertedValues_TranslatesAndFindsTheRows()
        {
            using var db = CreateContext();

            Assert.Equal(["So What", "Blue in Green"], ConverterQueries.TracksInGenre(db, Genre.Jazz).OrderBy(t => t.Id).Select(t => t.Name).ToList());
            Assert.Equal("So What", ConverterQueries.TrackById(db, new TrackId(3)).Single().Name);
            Assert.Equal("Ada", ConverterQueries.CustomerByEmail(db, "ada@example.com").Single().Name);

            // The email parameter was encrypted before it was sent.
            Assert.DoesNotContain("ada@example.com", ConverterQueries.CustomerByEmail(db, "ada@example.com").ToQueryString());
        }

        [Fact]
        public void OrderingAnEnumStoredAsString_IsAlphabetical_NotDeclarationOrder()
        {
            using var db = CreateContext();

            var genres = ConverterQueries.TracksOrderedByGenre(db).Select(t => t.Genre).ToList();

            Assert.Equal([Genre.Blues, Genre.Classical, Genre.Jazz, Genre.Jazz, Genre.Metal, Genre.Rock, Genre.Rock], genres);
            Assert.NotEqual(genres.OrderBy(g => g), genres);
        }

        [Fact]
        public void GreaterThanOnAnEnumStoredAsString_ReturnsTheWrongRows()
        {
            using var db = CreateContext();

            var fromSql = ConverterQueries.TracksAfterGenre(db, Genre.Jazz).Select(t => t.Genre).ToList();
            var inMemory = db.Tracks.AsEnumerable().Where(t => t.Genre > Genre.Jazz).Select(t => t.Genre).ToList();

            // SQL compared text: 'Metal' and 'Rock' sort after 'Jazz'. C# compared numbers: Metal (2), Blues (3), Classical (4).
            Assert.Equal([Genre.Rock, Genre.Metal], fromSql.Distinct().Order());
            Assert.Equal([Genre.Metal, Genre.Blues, Genre.Classical], inMemory.Distinct().Order());
        }

        [Fact]
        public void StringFunctionsOnAnEncryptedColumn_MatchNothing_OrFail()
        {
            using var db = CreateContext();

            // Constant prefix: the whole LIKE pattern 'a%' is encrypted -> no rows, no error.
            Assert.Contains($"LIKE '{EncryptedStringConverter.Encrypt("a%")}'", ConverterQueries.CustomersWhoseEmailStartsWithA(db).ToQueryString());
            Assert.Empty(ConverterQueries.CustomersWhoseEmailStartsWithA(db).ToList());

            // Contains: an encrypted fragment is never inside the ciphertext.
            Assert.Empty(ConverterQueries.CustomersWhoseEmailContains(db, "example.org").ToList());

            // Variable prefix: the ESCAPE character is encrypted too and SQLite rejects it.
            var sql = ConverterQueries.CustomersWhoseEmailStartsWith(db, "a").ToQueryString();
            Assert.Contains($"ESCAPE '{EncryptedStringConverter.Encrypt("\\")}'", sql);
            var ex = Assert.Throws<SqliteException>(() => ConverterQueries.CustomersWhoseEmailStartsWith(db, "a").ToList());
            Assert.Contains("ESCAPE expression must be a single character", ex.Message);

            // Meanwhile two customers really do have an e-mail starting with "a".
            Assert.Equal(2, db.Customers.AsEnumerable().Count(c => c.Email.StartsWith('a')));
        }

        [Fact]
        public void OrderingAnEncryptedColumn_SortsByCiphertext()
        {
            using var db = CreateContext();

            var names = ConverterQueries.CustomersOrderedByEmail(db).Select(c => c.Name).ToList();

            Assert.Equal(["Grace", "Ada", "Alan"], names);
        }

        [Fact]
        public void MembersOfConvertedValues_AndConvertedLists_CannotBeTranslated()
        {
            using var db = CreateContext();

            var idEx = Assert.Throws<InvalidOperationException>(() => ConverterQueries.TracksWithIdAbove(db, 5).ToList());
            Assert.Contains("could not be translated", idEx.Message);

            var tagEx = Assert.Throws<InvalidOperationException>(() => ConverterQueries.TracksTagged(db, "single").ToList());
            Assert.Contains("could not be translated", tagEx.Message);
        }

        [Fact]
        public void PrimitiveCollection_WithoutAConverter_IsQueryable()
        {
            using var db = CreateContext();

            var query = ConverterQueries.TracksInMood(db, "calm");

            Assert.Contains("json_each", query.ToQueryString());
            Assert.Equal(["So What", "Blue in Green"], query.OrderBy(t => t.Id).Select(t => t.Name).ToList());
        }

        [Fact]
        public void MutatingAConvertedList_IsSaved_BecauseOfTheValueComparer()
        {
            using (var db = CreateContext())
            {
                db.Tracks.Single(t => t.Id == new TrackId(1)).Tags.Add("classic");
                Assert.Equal(1, db.SaveChanges());
            }

            using (var db = CreateContext())
            {
                Assert.Equal(["single", "remastered", "classic"], db.Tracks.Single(t => t.Id == new TrackId(1)).Tags);
            }
        }

        [Fact]
        public void MutatingAConvertedList_WithoutAValueComparer_IsSilentlyLost()
        {
            using (var db = new NoComparerContext(CatalogContext.CreateOptions(_file.ConnectionString)))
            {
                var track = db.Tracks.Single(t => t.Id == new TrackId(1));
                track.Tags.Add("classic");

                Assert.Equal(EntityState.Unchanged, db.Entry(track).State);
                Assert.Equal(0, db.SaveChanges());
            }

            using (var db = CreateContext())
            {
                Assert.Equal(["single", "remastered"], db.Tracks.Single(t => t.Id == new TrackId(1)).Tags);
            }
        }

        /// <summary>The demo model with the Tags comparer removed, to show why the demo sets one.</summary>
        private sealed class NoComparerContext(DbContextOptions<CatalogContext> options) : CatalogContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<Track>().Property(t => t.Tags).Metadata.SetValueComparer((ValueComparer?)null);
            }
        }
    }
}
