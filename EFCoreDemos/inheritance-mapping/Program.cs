using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace inheritance_mapping;

public abstract class MediaItem
{
    public int Id { get; set; }
    public string Title { get; set; }
}

public class Song : MediaItem
{
    public string Album { get; set; }
    public int DurationSec { get; set; }
}

public class Podcast : MediaItem
{
    public string Host { get; set; }
    public int EpisodeCount { get; set; }
}

public class Audiobook : MediaItem
{
    public string Author { get; set; }
    public int Chapters { get; set; }
}

public abstract class MediaContextBase : DbContext
{
    public DbSet<MediaItem> Items { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<Audiobook> Audiobooks { get; set; }
}

public class TphContext : MediaContextBase
{
    protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=tph.db");
    // TPH is the EF default - no extra mapping needed.
}

public class TptContext : MediaContextBase
{
    protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=tpt.db");
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Song>().ToTable("Song");
        mb.Entity<Podcast>().ToTable("Podcast");
        mb.Entity<Audiobook>().ToTable("Audiobook");
    }
}

public class TpcContext : MediaContextBase
{
    protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=tpc.db");
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<MediaItem>().UseTpcMappingStrategy();
        // TPC: every subtype writes its own table, so we can't share an AUTOINCREMENT
        // sequence. In real apps you'd use HiLo or a sequence; here we assign IDs by hand.
        mb.Entity<MediaItem>().Property(m => m.Id).ValueGeneratedNever();
        mb.Entity<Song>().ToTable("Song");
        mb.Entity<Podcast>().ToTable("Podcast");
        mb.Entity<Audiobook>().ToTable("Audiobook");
    }
}

public static class Program
{
    public static void Main()
    {
        foreach (var file in new[] { "tph.db", "tpt.db", "tpc.db" })
            if (File.Exists(file)) File.Delete(file);

        Run<TphContext>("TPH (Table-Per-Hierarchy, the EF default)");
        Run<TptContext>("TPT (Table-Per-Type, one table per class)");
        Run<TpcContext>("TPC (Table-Per-Concrete, no shared table)");

        Console.WriteLine();
        Console.WriteLine("== Summary ==");
        Console.WriteLine("  TPH  : 1 table.   Polymorphic reads use a Discriminator column. Sparse columns for unused subtype fields.");
        Console.WriteLine("  TPT  : N+1 tables. Joins on the base table for polymorphic reads. No NULL waste; more joins.");
        Console.WriteLine("  TPC  : N tables.   No base table at all. UNION ALL for polymorphic reads. Best perf for subtype-only queries.");
    }

    private static void Run<TContext>(string label) where TContext : MediaContextBase, new()
    {
        Console.WriteLine();
        Console.WriteLine($"== {label} ==");

        using (var db = new TContext())
        {
            db.Database.EnsureCreated();
            db.AddRange(
                new Song { Id = 1, Title = "Inject The Venom", Album = "For Those About To Rock", DurationSec = 220 },
                new Podcast { Id = 2, Title = "Code Talk #42", Host = "Woody", EpisodeCount = 42 },
                new Audiobook { Id = 3, Title = "Beyond Boundaries", Author = "Chris Woodruff", Chapters = 12 });
            db.SaveChanges();
        }

        PrintTables<TContext>();

        using (var db = new TContext())
        {
            Console.WriteLine();
            Console.WriteLine("  Polymorphic query:  db.Items.ToList()");
            Console.WriteLine("    SQL: " + Compact(db.Items.ToQueryString()));
            Console.WriteLine($"    Rows: {db.Items.Count()}");

            Console.WriteLine();
            Console.WriteLine("  Subtype-only query: db.Songs.ToList()");
            Console.WriteLine("    SQL: " + Compact(db.Songs.ToQueryString()));
            Console.WriteLine($"    Rows: {db.Songs.Count()}");
        }
    }

    private static void PrintTables<TContext>() where TContext : MediaContextBase, new()
    {
        using var db = new TContext();
        var connStr = db.Database.GetConnectionString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read()) tables.Add(reader.GetString(0));
        Console.WriteLine($"  Tables in DB: {string.Join(", ", tables)}");
    }

    private static string Compact(string sql) =>
        string.Join(' ', sql.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0));
}
