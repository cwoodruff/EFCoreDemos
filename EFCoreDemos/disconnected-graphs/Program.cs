using disconnected_graphs;
using disconnected_graphs.Chinook;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// The demo writes, so it works on a throwaway copy of the chinook.db next to the binary.
// Every run starts from the same data and the shipped database is never modified.
var dbPath = Path.Combine(Path.GetTempPath(), $"disconnected-graphs-{Guid.NewGuid():N}.db");
File.Copy(Path.Combine(AppContext.BaseDirectory, "chinook.db"), dbPath);

var sql = new List<string>();
var options = new DbContextOptionsBuilder<ChinookContext>()
    .UseSqlite($"Data Source={dbPath};Pooling=False")
    .LogTo(e => sql.Add(e), [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
    .Options;

try
{
    RunDemo();
}
finally
{
    SqliteConnection.ClearAllPools();
    File.Delete(dbPath);
}

return;

void RunDemo()
{
    Section("1. The API round trip: GET the graph, edit it on the client, PUT it back");
    var (albumId, payload) = Arrange();
    Console.WriteLine($"""
        GET /albums/{albumId} serializes Album + Tracks with a short-lived context. The client then:
          - renames '{DemoAlbum.Renamed}' to '{DemoAlbum.NewName}'
          - deletes '{DemoAlbum.Removed}' (it is simply missing from the list it sends back)
          - adds '{DemoAlbum.Added}' (no Id yet - the database will generate one)

        PUT body (Composer/MediaTypeId/GenreId/Bytes trimmed here for width):
        """);
    Console.WriteLine("  " + ShortJson(payload));
    Console.WriteLine();
    Console.WriteLine("""
        The server deserializes that JSON into new objects. No DbContext has ever seen them, so EF knows
        nothing about what changed. Each approach below starts from a freshly seeded album and the same edits.
        """);

    Section("2. Update(album): mark the whole graph Modified");
    (albumId, payload) = Arrange();
    Save(db => GraphSaves.Update(db, payload));
    ShowRows(albumId);
    Console.WriteLine("""
        Every entity with a key became Modified with EVERY column flagged, so the album and all three tracks
        are rewritten even though only one name changed. The new track (Id 0) became Added - Update treats
        an unset generated key as new. And 'Bridge' is still there: it isn't in the graph, so EF never
        heard about it. Bonus risk: any column the client didn't send comes back as null/default and is
        written over the real value.
        """);

    Section("3. Attach(album): everything Unchanged... including the edit");
    (albumId, payload) = Arrange();
    Save(db => GraphSaves.AttachOnly(db, payload));
    ShowRows(albumId);
    Console.WriteLine($"""
        Attach marks keyed entities Unchanged and Id-0 entities Added. Only the INSERT ran:
        the rename to '{DemoAlbum.NewName}' was silently lost.
        """);

    Console.WriteLine();
    Console.WriteLine("-- Attach + mark what changed (the client also sent: changedTrackIds = [...]) --");
    (albumId, payload) = Arrange();
    var renamedIds = payload.Tracks.Where(t => t.Name == DemoAlbum.NewName).Select(t => t.Id).ToList();
    Save(db => GraphSaves.AttachAndMark(db, payload, renamedIds));
    ShowRows(albumId);
    Console.WriteLine("""
        Precise: one UPDATE of one column, one INSERT. But only because the client told the server what it
        changed. Without that, the server can't know - the original values are gone with the old context.
        """);

    Section("4. ChangeTracker.TrackGraph: a callback decides each entity's state");
    (albumId, payload) = Arrange();
    Save(db => GraphSaves.TrackGraph(db, payload, line => Console.WriteLine("  " + line)));
    ShowRows(albumId);
    Console.WriteLine("""
        TrackGraph walks the graph once and hands every entity to your callback, so the rules are yours.
        Here: albums are read-only on this endpoint (Unchanged), tracks with a key are Modified, tracks
        without one are Added. Tracks are still rewritten column by column, and 'Bridge' still survives:
        TrackGraph can only visit what's in the graph.
        """);

    Section("5. Deleted children: invisible to Update, Attach and TrackGraph");
    Console.WriteLine($"""
        The client deleted '{DemoAlbum.Removed}' by leaving it out. A missing object can't carry an
        EntityState, so none of the approaches above produced a DELETE. The fix is load-then-diff: load what
        the database has, compare it with the payload, and apply the difference.

          var existing = db.Albums.Include(a => a.Tracks).Single(a => a.Id == payload.Id);
          db.Entry(existing).CurrentValues.SetValues(payload);
          foreach (var track in existing.Tracks.ToList())
              if (payload has track.Id) db.Entry(track).CurrentValues.SetValues(incoming);
              else db.Tracks.Remove(track);
          foreach (var track in payload.Tracks.Where(t => t.Id == 0)) existing.Tracks.Add(track);

        """);
    (albumId, payload) = Arrange();
    Save(db => GraphSaves.LoadThenDiff(db, payload));
    ShowRows(albumId);
    Console.WriteLine("""
        One extra SELECT buys the right answer: SetValues only flags values that actually differ, so the
        album isn't touched, one track gets a one-column UPDATE, the deleted track gets a DELETE and the
        new one an INSERT.
        """);

    Console.WriteLine();
    Console.WriteLine($"-- Careful how you delete: existing.Tracks.Remove(track) instead of db.Tracks.Remove(track) --");
    var orphanAlbumId = Seed();
    Save(db => GraphSaves.RemoveFromCollection(db, orphanAlbumId, DemoAlbum.Removed));
    ShowRows(orphanAlbumId);
    Console.WriteLine($"""
        Track.AlbumId is nullable in Chinook, so Album -> Track is an OPTIONAL relationship. Removing from the
        collection only severs it: EF sets AlbumId = NULL and '{DemoAlbum.Removed}' lives on as an orphan.
        Remove it from the DbSet to get a DELETE (or make the relationship required, and EF deletes orphans).
        """);

    Section("6. Key-value detection: how EF decides Added vs Modified");
    (albumId, payload) = Arrange();
    using (var db = new ChinookContext(options))
    {
        var brandNew = new Track { Name = "Id 0", AlbumId = albumId, UnitPrice = 0.99m };
        var existing = new Track { Id = payload.Tracks[0].Id, Name = "Id set", AlbumId = albumId, UnitPrice = 0.99m };
        var ghost = new Track { Id = 999_999, Name = "Id set, no such row", AlbumId = albumId, UnitPrice = 0.99m };

        db.Tracks.UpdateRange(brandNew, existing, ghost);
        foreach (var entry in db.ChangeTracker.Entries<Track>())
        {
            Console.WriteLine($"  Update(Track {{ Id = {entry.Entity.Id,7} }})  key set: {entry.IsKeySet,-5} -> {entry.State}");
        }

        Console.WriteLine();
        Console.WriteLine("""
            The rule is just "is the generated key set?" - 0 means Added, anything else means Modified.
            EF doesn't check the database, so the ghost row is happily marked Modified...
            """);
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Console.WriteLine($"  SaveChanges -> {ex.GetType().Name}:");
            Console.WriteLine($"  {ex.Message.Split(". ")[0]}.");
        }
    }

    Console.WriteLine();
    Console.WriteLine("""
        ...and its UPDATE matches zero rows. EF treats "0 rows affected" as a concurrency conflict and rolls
        the whole SaveChanges back. A client that invents ids, or a row deleted by someone else in the
        meantime, both end up here.
        """);

    Section("Which one?");
    Console.WriteLine("""
        Update()          Simple, but rewrites every column of every entity, and never deletes.
        Attach() + mark   Minimal SQL, but only if the client says exactly what changed.
        TrackGraph()      Your rules per entity (e.g. read-only parents); still blind to deletions.
        Load-then-diff    One extra SELECT; the only one that gets updates, inserts AND deletes right.
                          The default choice for "PUT the whole graph" endpoints.
        """);
}

(int AlbumId, Album Payload) Arrange()
{
    var albumId = Seed();
    using var db = new ChinookContext(options);
    return (albumId, Client.EditAndPut(Client.Get(db, albumId)));
}

int Seed()
{
    using var db = new ChinookContext(options);
    return DemoAlbum.Seed(db);
}

void Save(Action<ChinookContext> save)
{
    using var db = new ChinookContext(options);
    db.SavingChanges += (_, _) =>
    {
        Console.WriteLine("Tracked before SaveChanges:");
        foreach (var entry in db.ChangeTracker.Entries())
        {
            var modified = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
            var detail = entry.State == EntityState.Modified ? $"  modified: {string.Join(", ", modified)}" : "";
            Console.WriteLine($"  {entry.Metadata.ClrType.Name,-6} {GraphSaves.Describe(entry.Entity),-34} {entry.State}{detail}");
        }

        Console.WriteLine();
    };

    sql.Clear();
    save(db);

    var commands = sql.Where(e => e.Contains("Executed DbCommand")).Select(SqlOf).ToList();
    Console.WriteLine($"SQL ({commands.Count} command(s)):");
    foreach (var command in commands)
    {
        Console.WriteLine(Indent(command));
    }
}

void ShowRows(int albumId)
{
    using var db = new ChinookContext(options);
    var names = DemoAlbum.SeedTracks.Append(DemoAlbum.NewName).Append(DemoAlbum.Added).ToList();
    var tracks = db.Tracks.AsNoTracking()
        .Where(t => t.AlbumId == albumId || (t.AlbumId == null && names.Contains(t.Name!)))
        .OrderBy(t => t.Id)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Database afterwards (album #{albumId}):");
    foreach (var t in tracks)
    {
        var note = t.AlbumId is null ? "   <- orphan: AlbumId is NULL"
            : t.Name == DemoAlbum.Removed ? "   <- deleted by the client, still here"
            : "";
        Console.WriteLine($"  #{t.Id,-5} {t.Name,-20} AlbumId={t.AlbumId?.ToString() ?? "NULL"}{note}");
    }

    Console.WriteLine();
}

static string ShortJson(Album album) =>
    $"{{\"Id\":{album.Id},\"Title\":\"{album.Title}\",\"Tracks\":[" +
    string.Join(",", album.Tracks.Select(t => $"{{\"Id\":{t.Id},\"Name\":\"{t.Name}\",...}}")) + "]}";

// "info: ... \n Executed DbCommand (0ms) [Parameters=...]\n <sql>" -> just the SQL.
static string SqlOf(string logEntry)
{
    var lines = logEntry.Split('\n')
        .SkipWhile(l => !l.Contains("Executed DbCommand")).Skip(1)
        .Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
    var indent = lines.Count == 0 ? 0 : lines.Min(l => l.Length - l.TrimStart().Length);
    return string.Join('\n', lines.Select(l => l[indent..]));
}

static string Indent(string text) => string.Join('\n', text.Split('\n').Select(l => "  " + l));

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 100));
}
