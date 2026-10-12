using System.Text.Json;
using disconnected_graphs.Chinook;
using Microsoft.EntityFrameworkCore;

namespace disconnected_graphs;

/// <summary>The demo album every scenario starts from, and the edits the "client" makes to it.</summary>
public static class DemoAlbum
{
    public const string Title = "Disconnected Graphs (Demo)";
    public static readonly string[] SeedTracks = ["Intro", "Verse", "Bridge", "Outro"];

    public const string Renamed = "Verse";
    public const string NewName = "Verse (Remastered)";
    public const string Removed = "Bridge";
    public const string Added = "Hidden Track";

    /// <summary>Inserts a fresh album with four tracks and returns its id. Every scenario gets its own copy.</summary>
    public static int Seed(ChinookContext db)
    {
        var album = new Album
        {
            Title = Title,
            ArtistId = 1,
            Tracks = SeedTracks.Select((name, i) => new Track
            {
                Name = name,
                MediaTypeId = 1,
                GenreId = 1,
                Composer = "EF Core Team",
                Milliseconds = 180_000 + i * 15_000,
                Bytes = 6_000_000 + i * 500_000,
                UnitPrice = 0.99m
            }).ToList()
        };
        db.Albums.Add(album);
        db.SaveChanges();
        return album.Id;
    }
}

/// <summary>
/// The API boundary. GET serializes the graph to JSON; the client edits its own copy and PUTs it back.
/// What reaches the server is a brand-new object graph that no DbContext has ever tracked.
/// </summary>
public static class Client
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>GET /albums/{id}: load with a short-lived context and serialize.</summary>
    public static string Get(ChinookContext db, int albumId) =>
        JsonSerializer.Serialize(
            db.Albums.AsNoTracking().Include(a => a.Tracks.OrderBy(t => t.Id)).Single(a => a.Id == albumId),
            Json);

    /// <summary>The client renames one track, deletes one and adds one, then PUTs the whole album back.</summary>
    public static Album EditAndPut(string json)
    {
        var album = JsonSerializer.Deserialize<Album>(json, Json)!;

        album.Tracks.Single(t => t.Name == DemoAlbum.Renamed).Name = DemoAlbum.NewName;
        album.Tracks.RemoveAll(t => t.Name == DemoAlbum.Removed);
        album.Tracks.Add(new Track
        {
            // No Id: the database generates it. Id == 0 is how EF recognizes a new row.
            Name = DemoAlbum.Added,
            AlbumId = album.Id,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "EF Core Team",
            Milliseconds = 240_000,
            Bytes = 8_000_000,
            UnitPrice = 0.99m
        });

        return album;
    }

    public static string Serialize(Album album) => JsonSerializer.Serialize(album, Json);
}

/// <summary>
/// Ways a server can save the graph the client sent back. Each takes a NEW context, as a web request would.
/// </summary>
public static class GraphSaves
{
    /// <summary>Update(): every entity with a key becomes Modified (all columns), key 0 becomes Added.</summary>
    public static void Update(ChinookContext db, Album payload)
    {
        db.Albums.Update(payload);
        db.SaveChanges();
    }

    /// <summary>Attach(): keyed entities become Unchanged, key 0 becomes Added. Edits are NOT saved.</summary>
    public static void AttachOnly(ChinookContext db, Album payload)
    {
        db.Albums.Attach(payload);
        db.SaveChanges();
    }

    /// <summary>
    /// Attach(), then mark exactly what changed. Only works if the server knows what changed -
    /// here the client sent a "changed track ids" list alongside the graph.
    /// </summary>
    public static void AttachAndMark(ChinookContext db, Album payload, IReadOnlyCollection<int> renamedTrackIds)
    {
        db.Albums.Attach(payload);
        foreach (var track in payload.Tracks.Where(t => renamedTrackIds.Contains(t.Id)))
        {
            db.Entry(track).Property(t => t.Name).IsModified = true;
        }

        db.SaveChanges();
    }

    /// <summary>
    /// TrackGraph: EF walks the graph and asks a callback for each entity's state. Here the policy is
    /// "this endpoint edits tracks, not albums": the album stays Unchanged, tracks are Modified or Added.
    /// </summary>
    public static void TrackGraph(ChinookContext db, Album payload, Action<string>? trace = null)
    {
        db.ChangeTracker.TrackGraph(payload, node =>
        {
            var entry = node.Entry;
            entry.State = entry.Entity switch
            {
                Album => EntityState.Unchanged,
                Track when entry.IsKeySet => EntityState.Modified,
                Track => EntityState.Added,
                _ => EntityState.Unchanged
            };

            var from = node.SourceEntry is null ? "root" : $"via {node.SourceEntry.Metadata.ClrType.Name}.{node.InboundNavigation?.Name}";
            trace?.Invoke($"{entry.Metadata.ClrType.Name,-6} {Describe(entry.Entity),-30} key set: {entry.IsKeySet,-5}  -> {entry.State,-9} ({from})");
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Load-then-diff: load what the database has, compare it with the payload, and apply only the difference.
    /// The only approach of these that sees deletions, and the only one that sends just the changed columns
    /// without the client having to say what changed.
    /// </summary>
    public static void LoadThenDiff(ChinookContext db, Album payload)
    {
        var existing = db.Albums.Include(a => a.Tracks).Single(a => a.Id == payload.Id);

        db.Entry(existing).CurrentValues.SetValues(payload);

        var sent = payload.Tracks.Where(t => t.Id != 0).ToDictionary(t => t.Id);
        foreach (var track in existing.Tracks.ToList())
        {
            if (sent.TryGetValue(track.Id, out var incoming))
            {
                db.Entry(track).CurrentValues.SetValues(incoming); // only differing values become Modified
            }
            else
            {
                db.Tracks.Remove(track); // in the database, missing from the payload: the client deleted it
            }
        }

        foreach (var track in payload.Tracks.Where(t => t.Id == 0))
        {
            existing.Tracks.Add(track);
        }

        db.SaveChanges();
    }

    /// <summary>
    /// The load-then-diff mistake: removing the child from the navigation collection instead of the DbSet.
    /// AlbumId is nullable (an optional relationship), so EF orphans the row - AlbumId = NULL - rather than
    /// deleting it.
    /// </summary>
    public static void RemoveFromCollection(ChinookContext db, int albumId, string trackName)
    {
        var existing = db.Albums.Include(a => a.Tracks).Single(a => a.Id == albumId);
        existing.Tracks.Remove(existing.Tracks.Single(t => t.Name == trackName));
        db.SaveChanges();
    }

    public static string Describe(object entity) => entity switch
    {
        Album a => $"#{a.Id} '{a.Title}'",
        Track t => $"#{t.Id} '{t.Name}'",
        _ => entity.ToString() ?? ""
    };
}
