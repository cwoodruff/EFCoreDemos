using BenchmarkDotNet.Running;
using ef_core_sprocs_perf.Benchmarks;
using Microsoft.EntityFrameworkCore;
using sproc_mapping.Chinook;

// Default: show the two query paths side by side.
// Benchmark:  dotnet run -c Release -- benchmark
if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
{
    BenchmarkRunner.Run<AlbumQueryBenchmarks>(args: args[1..]);
    return;
}

var options = new DbContextOptionsBuilder<ChinookContext>()
    .UseSqlServer(AlbumQueryBenchmarks.ConnectionString)
    .Options;

await using var context = new ChinookContext(options);

var linqQuery = context.Albums.AsNoTracking();
var sprocQuery = context.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking();

Console.WriteLine("== LINQ ==");
Console.WriteLine(linqQuery.ToQueryString());
var linq = await linqQuery.ToListAsync();
Console.WriteLine($"-> {linq.Count} albums");
Console.WriteLine();

Console.WriteLine("== FromSql (stored procedure) ==");
Console.WriteLine(sprocQuery.ToQueryString());
var sproc = await sprocQuery.ToListAsync();
Console.WriteLine($"-> {sproc.Count} albums");
Console.WriteLine();

var same = linq.Select(a => (a.Id, a.Title, a.ArtistId)).OrderBy(a => a.Id)
    .SequenceEqual(sproc.Select(a => (a.Id, a.Title, a.ArtistId)).OrderBy(a => a.Id));
Console.WriteLine(same ? "Both paths returned identical rows." : "WARNING: the two paths returned different rows.");
Console.WriteLine();
Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");
