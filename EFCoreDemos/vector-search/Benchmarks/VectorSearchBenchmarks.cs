using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using vector_search.Data;
using vector_search.Embeddings;

namespace vector_search.Benchmarks;

/// <summary>
/// Top-10 nearest neighbours over ~3,500 track vectors: VECTOR_DISTANCE in SQL Server vs loading every
/// vector and ranking on the client. The phrase is embedded once in setup so only the search is timed;
/// "Embed phrase" shows what the in-process model adds per search on top of either.
/// </summary>
[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
public class VectorSearchBenchmarks
{
    public const string Phrase = "aggressive heavy metal";
    public const int Take = 10;

    private DbContextOptions<VectorDemoContext> _options = null!;
    private TextEmbedder _embedder = null!;
    private float[] _query = null!;
    private SqlVector<float> _sqlQuery;

    [GlobalSetup]
    public void Setup()
    {
        _embedder = TextEmbedder.CreateAsync(Console.Out).GetAwaiter().GetResult();
        VectorStore.EnsureSeededAsync(_embedder, Console.Out).GetAwaiter().GetResult();

        _options = VectorStore.Options();
        _query = _embedder.Embed(Phrase);
        _sqlQuery = new SqlVector<float>(_query);

        // Warm up and make sure both paths agree before timing them.
        var server = ServerSide().GetAwaiter().GetResult();
        var client = ClientSide().GetAwaiter().GetResult();

        if (!server.Select(r => r.TrackId).SequenceEqual(client.Select(r => r.TrackId)))
            throw new InvalidOperationException("Server-side and client-side search returned different tracks.");
    }

    [GlobalCleanup]
    public void Cleanup() => _embedder.Dispose();

    [Benchmark(Baseline = true, Description = "VectorDistance (server)")]
    public async Task<List<SearchResult>> ServerSide()
    {
        await using var db = new VectorDemoContext(_options);
        return await VectorStore.ServerSideQuery(db, _sqlQuery, Take).ToListAsync();
    }

    [Benchmark(Description = "Load all + rank (client)")]
    public async Task<List<SearchResult>> ClientSide()
    {
        await using var db = new VectorDemoContext(_options);
        return await VectorStore.ClientSideSearchAsync(db, _query, Take);
    }

    [Benchmark(Description = "Embed phrase (ONNX)")]
    public float[] EmbedPhrase() => _embedder.Embed(Phrase);
}
