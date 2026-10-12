using Microsoft.EntityFrameworkCore;
using shadow_properties_backing_fields.Model;

namespace shadow_properties_backing_fields;

internal class Program
{
    // A throwaway SQLite file next to the binaries, recreated on every run.
    private static readonly string Connection = $"Data Source={Path.Combine(AppContext.BaseDirectory, "music.db")}";

    // A fake clock so the audit timestamps are readable and repeatable.
    private static DateTime _now = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

    // SQL logging is switched on only for the steps where the SQL is the point.
    private static bool _showSql;

    private static MusicContext Db() => new(MusicContext.CreateOptions(Connection, Log), () => _now);

    private static void Log(string message)
    {
        if (_showSql) Console.WriteLine(message);
    }

    public static async Task Main()
    {
        // ------------------------------------------------------------------
        Header("1. What the domain classes look like vs what EF maps");
        Console.WriteLine("Album's public surface (reflection):");
        foreach (var p in typeof(Album).GetProperties())
        {
            var setter = p.SetMethod is null ? "no setter" : p.SetMethod.IsPublic ? "public set" : "private set";
            var type = p.PropertyType.IsGenericType
                ? $"{p.PropertyType.Name.Split('`')[0]}<{string.Join(", ", p.PropertyType.GetGenericArguments().Select(a => a.Name))}>"
                : p.PropertyType.Name;
            Console.WriteLine($"  {type,-28} {p.Name,-8} ({setter})");
        }

        Console.WriteLine();
        Console.WriteLine("EF's model of the same types:");
        await using (var db = Db())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            foreach (var entityType in db.Model.GetEntityTypes())
            {
                Console.WriteLine($"  {entityType.DisplayName()} -> table {entityType.GetTableName()}");
                foreach (var p in entityType.GetProperties())
                {
                    var kind = p.IsShadowProperty() ? "SHADOW" : p.FieldInfo is { } f && p.PropertyInfo is null ? $"field {f.Name}" : "property";
                    var facets = p.GetMaxLength() is { } max ? $"max length {max}"
                        : p.GetPrecision() is { } precision ? $"precision ({precision},{p.GetScale()})"
                        : "";
                    var type = Nullable.GetUnderlyingType(p.ClrType) is { } inner ? inner.Name + "?" : p.ClrType.Name;
                    Console.WriteLine($"    {p.Name,-12} {type,-9} {kind,-9} {facets}");
                }

                foreach (var n in entityType.GetNavigations())
                {
                    Console.WriteLine($"    {n.Name,-12} navigation via field {n.FieldInfo?.Name} (access mode {n.GetPropertyAccessMode()})");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Neither class declares CreatedAt, LastUpdated or AlbumId - EF added them as shadow properties.");
        Console.WriteLine("Every string got max length 200 and every decimal (10,2) from ConfigureConventions;");
        Console.WriteLine("Album.Title's explicit HasMaxLength(160) overrides the convention.");

        // ------------------------------------------------------------------
        Header("2. The migration EF would generate (scaffolded in-process, nothing written to disk)");
        await using (var db = Db())
        {
            var migration = MigrationPreview.ScaffoldInitial(db);
            Console.WriteLine(TrimMigration(migration.MigrationCode));
        }

        Console.WriteLine();
        Console.WriteLine("The SQL SQLite actually gets (GenerateCreateScript):");
        await using (var db = Db())
        {
            Console.WriteLine(db.Database.GenerateCreateScript().Trim());
        }

        Console.WriteLine();
        Console.WriteLine("SQLite has no varchar(n) or decimal(p,s), so the DDL says TEXT. The migration (maxLength: 200,");
        Console.WriteLine("precision: 10, scale: 2) proves the conventions are in the model - SQL Server would get nvarchar(200)");
        Console.WriteLine("and decimal(10,2) from the same model.");

        // ------------------------------------------------------------------
        Header("3. Backing field: the collection only changes through the domain");
        int albumId;
        await using (var db = Db())
        {
            var album = new Album("  Kind of Blue ");
            album.AddTrack("So What", 0.99m);
            album.AddTrack("Freddie Freeloader", 0.99m);
            album.AddTrack("Blue in Green", 0.99m);

            Try("album.AddTrack(\"So What\", 0.99m)", () => album.AddTrack("So What", 0.99m));
            Try("((ICollection<Track>)album.Tracks).Clear()", () => ((ICollection<Track>)album.Tracks).Clear());
            Try("((IList<Track>)album.Tracks).Add(...)", () => ((IList<Track>)album.Tracks).Add(new Track("Bonus", 0m)));
            Try("new Album(\"\")", () => new Album(""));

            db.Albums.Add(album);
            _showSql = true;
            await db.SaveChangesAsync();
            _showSql = false;
            albumId = album.Id;
            Console.WriteLine($"Saved '{album.Title}' (title trimmed by Rename) with {album.Tracks.Count} tracks.");
        }

        await using (var db = Db())
        {
            var album = await db.Albums.Include(a => a.Tracks).SingleAsync(a => a.Id == albumId);
            Console.WriteLine($"Reloaded in a new context: '{album.Title}' with {album.Tracks.Count} tracks -");
            Console.WriteLine("EF filled _tracks directly and built Album through its constructor (no parameterless ctor).");
        }

        // ------------------------------------------------------------------
        Header("4. Shadow properties: audit columns set in SaveChanges");
        await using (var db = Db())
        {
            db.Albums.Add(new Album("A Love Supreme"));
            await db.SaveChangesAsync();
        }

        _now = _now.AddDays(3).AddHours(5);
        await using (var db = Db())
        {
            var album = await db.Albums.SingleAsync(a => a.Id == albumId);
            album.Rename("Kind of Blue (Legacy Edition)");

            _showSql = true;
            await db.SaveChangesAsync();
            _showSql = false;

            var entry = db.Entry(album);
            Console.WriteLine($"CreatedAt   {entry.Property(MusicContext.CreatedAt).CurrentValue:yyyy-MM-dd HH:mm}  (unchanged)");
            Console.WriteLine($"LastUpdated {entry.Property(MusicContext.LastUpdated).CurrentValue:yyyy-MM-dd HH:mm}  (stamped by SaveChanges)");
        }

        // ------------------------------------------------------------------
        Header("5. Querying shadow properties with EF.Property<T>");
        await using (var db = Db())
        {
            var since = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc);
            var query = db.Albums
                .Where(a => EF.Property<DateTime>(a, MusicContext.LastUpdated) >= since)
                .OrderByDescending(a => EF.Property<DateTime>(a, MusicContext.LastUpdated))
                .Select(a => new
                {
                    a.Title,
                    Created = EF.Property<DateTime>(a, MusicContext.CreatedAt),
                    Updated = EF.Property<DateTime>(a, MusicContext.LastUpdated),
                    TrackCount = a.Tracks.Count
                });

            Console.WriteLine(query.ToQueryString());
            Console.WriteLine();
            Console.WriteLine($"Albums changed since {since:yyyy-MM-dd}:");
            foreach (var row in await query.ToListAsync())
            {
                Console.WriteLine($"  {row.Title,-32} created {row.Created:yyyy-MM-dd HH:mm}, updated {row.Updated:yyyy-MM-dd HH:mm}, {row.TrackCount} tracks");
            }

            var untouched = await db.Albums
                .Where(a => EF.Property<DateTime>(a, MusicContext.LastUpdated) == EF.Property<DateTime>(a, MusicContext.CreatedAt))
                .Select(a => a.Title)
                .ToListAsync();
            Console.WriteLine($"Never modified since insert: [{string.Join(", ", untouched)}]");
        }

        Console.WriteLine();
        Console.WriteLine("Takeaway: Album and Track stay plain domain classes. Keys, audit columns, the backing field and");
        Console.WriteLine("column facets are all persistence decisions made in MusicContext.");
    }

    private static void Try(string label, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"  {label,-44} -> allowed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {label,-44} -> {ex.GetType().Name}: {ex.Message.Split('\n')[0].Trim()}");
        }
    }

    // Keep the scaffolded Up() method; Down() and the usings are noise on a slide.
    private static string TrimMigration(string code)
    {
        var lines = code.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.FindIndex(l => l.Contains("protected override void Up("));
        var end = lines.FindIndex(l => l.Contains("protected override void Down("));
        if (start < 0 || end < 0) return code;
        while (end > start && (string.IsNullOrWhiteSpace(lines[end - 1]) || lines[end - 1].TrimStart().StartsWith("///"))) end--;
        return string.Join('\n', lines[start..end]).TrimEnd();
    }

    private static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}
