using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace optimistic_concurrency;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int Version { get; set; }  // bumped by interceptor, marked as concurrency token below.
}

public class DocsContext : DbContext
{
    public const string ConnectionString = "Data Source=concurrency.db";

    public DbSet<Document> Documents { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder o)
    {
        if (!o.IsConfigured)
        {
            o.UseSqlite(ConnectionString)
             .AddInterceptors(new BumpVersionInterceptor());
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Document>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Title).IsRequired().HasMaxLength(200);
            e.Property(d => d.Body).IsRequired();
            // The portable equivalent of [Timestamp] - works on every provider.
            e.Property(d => d.Version).IsConcurrencyToken();
        });
    }
}

// Increment Version on every Modified entity right before SaveChanges issues SQL.
// EF includes the OLD Version in the UPDATE's WHERE; if the row was bumped since
// we read it, rowcount=0 and EF throws DbUpdateConcurrencyException.
public sealed class BumpVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Bump(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Bump(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void Bump(DbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker.Entries<Document>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version += 1;
            }
        }
    }
}

public static class Program
{
    public static void Main()
    {
        const string dbFile = "concurrency.db";
        if (File.Exists(dbFile)) File.Delete(dbFile);

        Seed();

        Console.WriteLine("== A and B both load Document #1 ==");
        using var ctxA = new DocsContext();
        using var ctxB = new DocsContext();
        var docA = ctxA.Documents.Single(d => d.Id == 1);
        var docB = ctxB.Documents.Single(d => d.Id == 1);
        Console.WriteLine($"  A sees: Version={docA.Version}, Title='{docA.Title}'");
        Console.WriteLine($"  B sees: Version={docB.Version}, Title='{docB.Title}'");

        Console.WriteLine();
        Console.WriteLine("== A edits Title and saves (succeeds) ==");
        docA.Title = "Edited by A";
        ctxA.SaveChanges();
        Console.WriteLine($"  A saved. Row Version now = {docA.Version}");

        Console.WriteLine();
        Console.WriteLine("== B edits Body and tries to save (FAILS - stale Version) ==");
        docB.Body = "Edited by B";
        try
        {
            ctxB.SaveChanges();
            Console.WriteLine("  (unexpected: no exception)");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var dbValues = entry.GetDatabaseValues();
            Console.WriteLine($"  DbUpdateConcurrencyException - B's Version={entry.OriginalValues["Version"]}, DB Version={dbValues!["Version"]}.");

            Console.WriteLine();
            Console.WriteLine("  --- Resolution 1: ABORT ---");
            Console.WriteLine("    Reload, surface the conflict to the user, let them retry.");

            Console.WriteLine();
            Console.WriteLine("  --- Resolution 2: CLIENT WINS (force overwrite) ---");
            Console.WriteLine("    Copy OriginalValues from the database -> next SaveChanges will succeed.");

            Console.WriteLine();
            Console.WriteLine("  --- Resolution 3: THREE-WAY MERGE (chosen for this demo) ---");

            // Take what's in DB, layer B's intended Body change on top, leave A's Title.
            entry.OriginalValues.SetValues(dbValues);
            entry.CurrentValues["Body"] = "Edited by B";
            entry.CurrentValues["Title"] = dbValues["Title"]; // keep A's title

            ctxB.SaveChanges();

            using var verify = new DocsContext();
            var final = verify.Documents.Single(d => d.Id == 1);
            Console.WriteLine($"    After merge -> Version={final.Version}, Title='{final.Title}', Body='{final.Body}'");
        }
    }

    private static void Seed()
    {
        using var db = new DocsContext();
        db.Database.EnsureCreated();
        db.Documents.Add(new Document { Title = "Original", Body = "Initial body", Version = 1 });
        db.SaveChanges();
    }
}
