using System.Diagnostics;
using System.Numerics.Tensors;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using vector_search.Embeddings;

namespace vector_search.Data;

public sealed record SearchResult(int TrackId, string Track, string Artist, string Album, string Genre, double Distance);

/// <summary>Seeding the VectorDemo database and the two ways of searching it.</summary>
public static class VectorStore
{
    private const string Server =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;TrustServerCertificate=True;Application Name=EFCoreDemos-VectorSearch;";

    public const string ChinookConnectionString = Server + "Database=Chinook;";
    public const string ConnectionString = Server + "Database=VectorDemo;";

    public static DbContextOptions<VectorDemoContext> Options(string connectionString = ConnectionString) =>
        new DbContextOptionsBuilder<VectorDemoContext>().UseSqlServer(connectionString).Options;

    private sealed record TrackSource(int TrackId, string Track, string Artist, string Album, string Genre);

    /// <summary>
    /// Creates VectorDemo and embeds every Chinook track the first time; afterwards it's a no-op.
    /// Returns the number of tracks embedded by this call (0 when already seeded).
    /// </summary>
    public static async Task<int> EnsureSeededAsync(TextEmbedder embedder, TextWriter? log = null,
        string connectionString = ConnectionString)
    {
        await using var chinook = new DbContext(
            new DbContextOptionsBuilder().UseSqlServer(ChinookConnectionString).Options);

        var tracks = await chinook.Database.SqlQuery<TrackSource>($"""
            SELECT t.Id AS TrackId, t.Name AS Track, ar.Name AS Artist, al.Title AS Album, ISNULL(g.Name, N'Unknown') AS Genre
            FROM dbo.Track AS t
            JOIN dbo.Album AS al ON al.Id = t.AlbumId
            JOIN dbo.Artist AS ar ON ar.Id = al.ArtistId
            LEFT JOIN dbo.Genre AS g ON g.Id = t.GenreId
            """).ToListAsync();

        await using var db = new VectorDemoContext(Options(connectionString));
        await db.Database.EnsureCreatedAsync();

        if (await db.TrackEmbeddings.CountAsync() == tracks.Count) return 0;

        log?.WriteLine($"Embedding {tracks.Count:N0} Chinook tracks with {ModelFiles.Repo} (first run only) ...");
        var stopwatch = Stopwatch.StartNew();

        await db.TrackEmbeddings.ExecuteDeleteAsync();

        foreach (var chunk in tracks.Chunk(64))
        {
            var texts = chunk.Select(t => $"{t.Track} by {t.Artist} from the album {t.Album}, genre {t.Genre}").ToArray();
            var vectors = embedder.Embed(texts);

            db.TrackEmbeddings.AddRange(chunk.Select((t, i) => new TrackEmbedding
            {
                TrackId = t.TrackId,
                Track = t.Track,
                Artist = t.Artist,
                Album = t.Album,
                Genre = t.Genre,
                Text = texts[i],
                Embedding = new SqlVector<float>(vectors[i]),
            }));
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log?.WriteLine($"Stored {tracks.Count:N0} vectors in {stopwatch.Elapsed.TotalSeconds:N1}s.");
        return tracks.Count;
    }

    /// <summary>Nearest neighbours computed by SQL Server: VECTOR_DISTANCE + ORDER BY + TOP.</summary>
    public static IQueryable<SearchResult> ServerSideQuery(VectorDemoContext db, SqlVector<float> query, int take) =>
        db.TrackEmbeddings
            .OrderBy(t => EF.Functions.VectorDistance("cosine", t.Embedding, query))
            .Take(take)
            .Select(t => new SearchResult(t.TrackId, t.Track, t.Artist, t.Album, t.Genre,
                EF.Functions.VectorDistance("cosine", t.Embedding, query)));

    /// <summary>The alternative: pull every vector over the wire and rank on the client.</summary>
    public static async Task<List<SearchResult>> ClientSideSearchAsync(VectorDemoContext db, float[] query, int take)
    {
        var all = await db.TrackEmbeddings.AsNoTracking().ToListAsync();

        return all
            .Select(t => new SearchResult(t.TrackId, t.Track, t.Artist, t.Album, t.Genre,
                1 - TensorPrimitives.CosineSimilarity(t.Embedding.Memory.Span, query)))
            .OrderBy(r => r.Distance)
            .Take(take)
            .ToList();
    }
}
