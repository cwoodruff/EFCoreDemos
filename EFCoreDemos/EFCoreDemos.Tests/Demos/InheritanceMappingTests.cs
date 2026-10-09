extern alias inheritance_mapping;

using EFCoreDemos.Tests.Infrastructure;
using inheritance_mapping::inheritance_mapping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// inheritance-mapping: the same MediaItem hierarchy mapped as TPH (one table + discriminator),
    /// TPT (one table per type, joined) and TPC (one table per concrete type, UNION ALL).
    /// Each test redirects the demo context (hard-coded tph.db/tpt.db/tpc.db) to a temp file.
    /// </summary>
    public class InheritanceMappingTests
    {
        [Fact]
        public void Tph_MapsWholeHierarchyToOneTableWithDiscriminator()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(new TphContext(), temp);

            var model = db.Model;
            var root = model.FindEntityType(typeof(MediaItem))!;
            Assert.Equal("Discriminator", root.FindDiscriminatorProperty()?.Name);

            var rootTable = root.GetTableName();
            Assert.Equal(rootTable, model.FindEntityType(typeof(Song))!.GetTableName());
            Assert.Equal(rootTable, model.FindEntityType(typeof(Podcast))!.GetTableName());
            Assert.Equal(rootTable, model.FindEntityType(typeof(Audiobook))!.GetTableName());

            db.Database.EnsureCreated();
            Assert.Equal([rootTable!], UserTables(temp));
            Assert.Contains("Discriminator", Columns(temp, rootTable!));
        }

        [Fact]
        public void Tpt_MapsEveryTypeIncludingTheBaseToItsOwnTable()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(new TptContext(), temp);

            var model = db.Model;
            var root = model.FindEntityType(typeof(MediaItem))!;
            Assert.Null(root.FindDiscriminatorProperty());
            Assert.Equal("Song", model.FindEntityType(typeof(Song))!.GetTableName());
            Assert.Equal("Podcast", model.FindEntityType(typeof(Podcast))!.GetTableName());
            Assert.Equal("Audiobook", model.FindEntityType(typeof(Audiobook))!.GetTableName());

            db.Database.EnsureCreated();
            var tables = UserTables(temp);
            Assert.Equal(4, tables.Count);
            Assert.Contains(root.GetTableName()!, tables);

            // Subtype tables hold only their own columns; Title lives on the base table.
            var songColumns = Columns(temp, "Song");
            Assert.Contains("Album", songColumns);
            Assert.DoesNotContain("Title", songColumns);
            Assert.Contains("Title", Columns(temp, root.GetTableName()!));
        }

        [Fact]
        public void Tpc_MapsOnlyConcreteTypesWithNoBaseTable()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(new TpcContext(), temp);

            var root = db.Model.FindEntityType(typeof(MediaItem))!;
            Assert.Equal(RelationalAnnotationNames.TpcMappingStrategy, root.GetMappingStrategy());
            Assert.Null(root.GetTableName());

            db.Database.EnsureCreated();
            Assert.Equal(["Audiobook", "Podcast", "Song"], UserTables(temp));

            // Each concrete table carries the inherited Title column itself.
            Assert.Contains("Title", Columns(temp, "Song"));
            Assert.Contains("Title", Columns(temp, "Podcast"));
            Assert.Contains("Title", Columns(temp, "Audiobook"));
        }

        [Fact]
        public void PolymorphicQueries_ReturnDerivedInstances_ForAllStrategies()
        {
            AssertPolymorphicRoundTrip(() => new TphContext());
            AssertPolymorphicRoundTrip(() => new TptContext());
            AssertPolymorphicRoundTrip(() => new TpcContext());
        }

        [Fact]
        public void PolymorphicQuerySql_ReflectsTheStrategy()
        {
            using var temp = new TempSqliteFile();

            using (var tph = Open(new TphContext(), temp))
            {
                var all = tph.Items.ToQueryString();
                Assert.DoesNotContain("JOIN", all);
                Assert.DoesNotContain("UNION", all);
                Assert.Contains("Discriminator", tph.Songs.ToQueryString());
            }

            using (var tpt = Open(new TptContext(), temp))
            {
                Assert.Contains("LEFT JOIN", tpt.Items.ToQueryString());
            }

            using (var tpc = Open(new TpcContext(), temp))
            {
                var all = tpc.Items.ToQueryString();
                Assert.Contains("UNION ALL", all);

                // Subtype-only query touches just the Song table.
                var songs = tpc.Songs.ToQueryString();
                Assert.DoesNotContain("UNION", songs);
                Assert.Contains("\"Song\"", songs);
            }
        }

        private static void AssertPolymorphicRoundTrip<TContext>(Func<TContext> create) where TContext : MediaContextBase
        {
            using var temp = new TempSqliteFile();

            using (var db = Open(create(), temp))
            {
                db.Database.EnsureCreated();
                db.AddRange(
                    new Song { Id = 1, Title = "Inject The Venom", Album = "For Those About To Rock", DurationSec = 220 },
                    new Podcast { Id = 2, Title = "Code Talk #42", Host = "Woody", EpisodeCount = 42 },
                    new Audiobook { Id = 3, Title = "Beyond Boundaries", Author = "Chris Woodruff", Chapters = 12 });
                db.SaveChanges();
            }

            using (var db = Open(create(), temp))
            {
                var items = db.Items.OrderBy(i => i.Id).ToList();
                Assert.Collection(items,
                    i => Assert.Equal(220, Assert.IsType<Song>(i).DurationSec),
                    i => Assert.Equal("Woody", Assert.IsType<Podcast>(i).Host),
                    i => Assert.Equal(12, Assert.IsType<Audiobook>(i).Chapters));

                Assert.Equal("For Those About To Rock", Assert.Single(db.Songs.ToList()).Album);
                Assert.Equal(1, db.Items.OfType<Podcast>().Count());
            }
        }

        private static T Open<T>(T context, TempSqliteFile temp) where T : DbContext
        {
            context.Database.SetConnectionString(temp.ConnectionString);
            return context;
        }

        private static List<string> UserTables(TempSqliteFile temp) =>
            Query(temp, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory' ORDER BY name;");

        private static List<string> Columns(TempSqliteFile temp, string table) =>
            Query(temp, $"SELECT name FROM pragma_table_info('{table}');");

        private static List<string> Query(TempSqliteFile temp, string sql)
        {
            using var conn = new SqliteConnection(temp.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var result = new List<string>();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }
    }
}
