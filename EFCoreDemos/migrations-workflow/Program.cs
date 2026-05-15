using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using migrations_workflow;

const string DbFile = "migrations.db";
if (File.Exists(DbFile)) File.Delete(DbFile);

using var db = new ArticleContext();
var migrator = db.GetService<IMigrator>();

Console.WriteLine("== Pending migrations ==");
foreach (var m in db.Database.GetPendingMigrations())
{
    Console.WriteLine($"  {m}");
}

Console.WriteLine();
Console.WriteLine("== Step 1: apply Initial only ==");
var initial = db.Database.GetPendingMigrations().First();
migrator.Migrate(initial);
PrintArticleColumns();
PrintIndexes();

Console.WriteLine();
Console.WriteLine("== Step 2: apply remaining migrations ==");
db.Database.Migrate();
PrintArticleColumns();
PrintIndexes();

Console.WriteLine();
Console.WriteLine("== Step 3: insert + read to prove the schema works ==");
db.Articles.Add(new Article
{
    Title = "Hello migrations",
    Body = "All three migrations applied.",
    PublishedAt = DateTime.UtcNow,
    Isbn = "978-0-13-468599-1"
});
db.SaveChanges();
var inserted = db.Articles.First();
Console.WriteLine($"  Inserted Article #{inserted.Id} '{inserted.Title}' (ISBN {inserted.Isbn})");

Console.WriteLine();
Console.WriteLine("== Step 4: SQL script for review / CI/CD ==");
Console.WriteLine("  (SQLite doesn't support idempotent scripts; SQL Server does -");
Console.WriteLine("   use MigrationsSqlGenerationOptions.Idempotent there.)");
var script = migrator.GenerateScript(fromMigration: null, toMigration: null);
foreach (var line in TruncateMiddle(script, headLines: 12, tailLines: 6))
{
    Console.WriteLine("  " + line);
}

Console.WriteLine();
Console.WriteLine("== Step 5: production-ready bundle (informational) ==");
Console.WriteLine("  $ dotnet ef migrations bundle --project migrations-workflow.csproj");
Console.WriteLine("  -> produces a self-contained 'efbundle' executable that applies pending");
Console.WriteLine("     migrations against any connection string. Ship it with your release.");

return;

void PrintArticleColumns()
{
    using var conn = new SqliteConnection(ArticleContext.ConnectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA table_info('Article');";
    using var reader = cmd.ExecuteReader();
    var cols = new List<string>();
    while (reader.Read())
    {
        var name = reader.GetString(1);
        var type = reader.GetString(2);
        var notNull = reader.GetInt32(3) == 1 ? " NOT NULL" : "";
        cols.Add($"{name} {type}{notNull}");
    }
    Console.WriteLine("  Article columns: " + string.Join(", ", cols));
}

void PrintIndexes()
{
    using var conn = new SqliteConnection(ArticleContext.ConnectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='Article' AND sql IS NOT NULL;";
    using var reader = cmd.ExecuteReader();
    var any = false;
    while (reader.Read())
    {
        Console.WriteLine($"  index: {reader.GetString(0)}  ({reader.GetString(1)})");
        any = true;
    }
    if (!any) Console.WriteLine("  (no indexes besides the primary key)");
}

static IEnumerable<string> TruncateMiddle(string text, int headLines, int tailLines)
{
    var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
    if (lines.Length <= headLines + tailLines) return lines;

    var head = lines.Take(headLines);
    var tail = lines.TakeLast(tailLines);
    var middle = new[] { $"... ({lines.Length - headLines - tailLines} more lines omitted) ..." };
    return head.Concat(middle).Concat(tail);
}
