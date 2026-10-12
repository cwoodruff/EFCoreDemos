using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using vector_search.Benchmarks;
using vector_search.Data;
using vector_search.Embeddings;

// Default: embed a few phrases and find the nearest Chinook tracks inside SQL Server 2025.
//   dotnet run -c Release -- "your own phrase"   searches for your phrase instead
// Benchmark:  dotnet run -c Release -- benchmark
if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
{
    BenchmarkRunner.Run<VectorSearchBenchmarks>(args: args[1..]);
    return;
}

string[] phrases = args.Length > 0
    ? [string.Join(' ', args)]
    : ["aggressive heavy metal", "classical piano", "latin samba from brazil"];

using var embedder = await TextEmbedder.CreateAsync(Console.Out);
await VectorStore.EnsureSeededAsync(embedder, Console.Out);

await using var db = new VectorDemoContext(VectorStore.Options());

Console.WriteLine();
Console.WriteLine($"Model: {ModelFiles.Repo} ({ModelFiles.Dimensions} dimensions, ONNX Runtime, in-process)");
Console.WriteLine($"Vectors: {await db.TrackEmbeddings.CountAsync():N0} tracks in VectorDemo.dbo.TrackEmbedding, column vector({ModelFiles.Dimensions})");
Console.WriteLine();

var showSql = true;
foreach (var phrase in phrases)
{
    // The phrase is embedded by the same model that produced the stored vectors.
    var query = new SqlVector<float>(embedder.Embed(phrase));
    var search = VectorStore.ServerSideQuery(db, query, take: 5);

    Console.WriteLine($"== \"{phrase}\" ==");
    if (showSql)
    {
        Console.WriteLine("SQL (VECTOR_DISTANCE runs inside SQL Server; only 5 rows come back):");
        // ToQueryString can't print a vector literal, so describe the parameter instead of showing sql_variant.
        Console.WriteLine(Regex.Replace(search.ToQueryString(), @"DECLARE @query sql_variant = .*;",
            $"DECLARE @query vector({ModelFiles.Dimensions}) = /* the phrase, embedded in-process */;"));
        Console.WriteLine();
        showSql = false;
    }

    Console.WriteLine($"  {"Distance",8}  {"Track",-34} {"Artist",-22} {"Genre",-12}");
    foreach (var r in await search.ToListAsync())
    {
        Console.WriteLine($"  {r.Distance,8:F4}  {Clip(r.Track, 34),-34} {Clip(r.Artist, 22),-22} {Clip(r.Genre, 12),-12}");
    }
    Console.WriteLine();
}

// Same answer the hard way: every vector over the wire, cosine distance computed in .NET.
var check = phrases[0];
var vector = embedder.Embed(check);
var server = await VectorStore.ServerSideQuery(db, new SqlVector<float>(vector), 5).ToListAsync();
var client = await VectorStore.ClientSideSearchAsync(db, vector, 5);
var same = server.Select(r => r.TrackId).SequenceEqual(client.Select(r => r.TrackId));
Console.WriteLine(same
    ? $"Client-side ranking of all vectors returned the same top 5 for \"{check}\" - but had to load every row to do it."
    : "WARNING: client-side ranking returned different tracks.");
Console.WriteLine();
Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");

static string Clip(string s, int max) => s.Length > max ? s[..(max - 3)] + "..." : s;
