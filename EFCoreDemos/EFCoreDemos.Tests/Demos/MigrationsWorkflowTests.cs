extern alias migrations_workflow;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using migrations_workflow::migrations_workflow;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// migrations-workflow: three migrations (Initial, AddPublishedAt, AddIsbnUnique) evolve the Article
    /// table; they can be applied step by step with IMigrator or all at once with Migrate(), and the
    /// snapshot matches the current model. ArticleContext's "migrations.db" is redirected to a temp file.
    /// </summary>
    public class MigrationsWorkflowTests
    {
        private const string Initial = "20260515134629_Initial";
        private const string AddPublishedAt = "20260515134701_AddPublishedAt";
        private const string AddIsbnUnique = "20260515134728_AddIsbnUnique";

        [Fact]
        public void Assembly_ContainsTheThreeMigrationsInOrder()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(temp);

            Assert.Equal([Initial, AddPublishedAt, AddIsbnUnique], db.Database.GetMigrations());
            Assert.Equal([Initial, AddPublishedAt, AddIsbnUnique], db.Database.GetPendingMigrations());
        }

        [Fact]
        public void Model_HasNoPendingChangesAgainstTheSnapshot()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(temp);

            Assert.False(db.Database.HasPendingModelChanges());
        }

        [Fact]
        public void MigratingToInitial_CreatesOnlyTheOriginalColumns()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(temp);

            db.GetService<IMigrator>().Migrate(Initial);

            Assert.Equal(["Id", "Title", "Body"], Columns(temp));
            Assert.Equal([Initial], db.Database.GetAppliedMigrations());
            Assert.Equal([AddPublishedAt, AddIsbnUnique], db.Database.GetPendingMigrations());
        }

        [Fact]
        public void MigrateAll_ProducesTheFinalSchemaWithUniqueIsbnIndex()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(temp);

            db.GetService<IMigrator>().Migrate(Initial);
            db.Database.Migrate();

            Assert.Equal(["Id", "Title", "Body", "PublishedAt", "Isbn"], Columns(temp));
            Assert.Empty(db.Database.GetPendingMigrations());
            Assert.Equal(["IX_Article_Isbn"], Query(temp, "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='Article' AND sql IS NOT NULL;"));
            Assert.Equal(["1"], Query(temp, "SELECT \"unique\" FROM pragma_index_list('Article') WHERE name = 'IX_Article_Isbn';"));
        }

        [Fact]
        public void MigratedSchema_WorksAndEnforcesUniqueIsbn()
        {
            using var temp = new TempSqliteFile();

            using (var db = Open(temp))
            {
                db.Database.Migrate();
                db.Articles.Add(new Article { Title = "Hello migrations", Body = "All three migrations applied.", PublishedAt = DateTime.UtcNow, Isbn = "978-0-13-468599-1" });
                db.SaveChanges();
            }

            using (var db = Open(temp))
            {
                var article = db.Articles.Single();
                Assert.Equal("Hello migrations", article.Title);
                Assert.Equal("978-0-13-468599-1", article.Isbn);
                Assert.NotNull(article.PublishedAt);

                db.Articles.Add(new Article { Title = "Duplicate", Body = "Same ISBN", Isbn = "978-0-13-468599-1" });
                Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            }
        }

        [Fact]
        public void GenerateScript_ContainsEveryMigrationStep()
        {
            using var temp = new TempSqliteFile();
            using var db = Open(temp);

            var script = db.GetService<IMigrator>().GenerateScript(fromMigration: null, toMigration: null);

            Assert.Contains("CREATE TABLE \"Article\"", script);
            Assert.Contains("ALTER TABLE \"Article\" ADD \"PublishedAt\"", script);
            Assert.Contains("CREATE UNIQUE INDEX \"IX_Article_Isbn\"", script);
            Assert.Contains(AddIsbnUnique, script);

            // Generating a script never touches the database.
            Assert.False(File.Exists(temp.Path));
        }

        private static ArticleContext Open(TempSqliteFile temp)
        {
            var db = new ArticleContext();
            db.Database.SetConnectionString(temp.ConnectionString);
            return db;
        }

        private static List<string> Columns(TempSqliteFile temp) =>
            Query(temp, "SELECT name FROM pragma_table_info('Article') ORDER BY cid;");

        private static List<string> Query(TempSqliteFile temp, string sql)
        {
            using var conn = new SqliteConnection(temp.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var result = new List<string>();
            while (reader.Read()) result.Add(Convert.ToString(reader.GetValue(0))!);
            return result;
        }
    }
}
