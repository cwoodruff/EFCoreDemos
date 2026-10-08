using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.EntityFrameworkCore;
using sproc_mapping.Chinook;

namespace ef_core_sprocs_perf.Benchmarks;

/// <summary>
/// Compares loading every Album through an EF Core LINQ query against loading the
/// same rows through the dbo.sproc_GetAlbum stored procedure via FromSql.
///
///   LINQ  : SELECT [a].[Id], [a].[ArtistId], [a].[Title] FROM [Album] AS [a]
///   Sproc : EXEC dbo.sproc_GetAlbum  (SELECT Id, Title, ArtistId FROM dbo.Album)
///
/// Each benchmark runs with and without change tracking so the cost of the
/// query path can be separated from the cost of materialization + tracking.
/// </summary>
[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class AlbumQueryBenchmarks
{
    private const string ConnectionString =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;Database=Chinook;TrustServerCertificate=True;Application Name=EFCoreDemos-SprocPerf;";

    private DbContextOptions<ChinookContext> _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        // No logger factory: console logging would dominate the measurements.
        _options = new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        // Warm up the model, connection pool and both query plans so the first
        // measured iteration doesn't pay for model building.
        using var context = new ChinookContext(_options);
        var linqCount = context.Albums.AsNoTracking().ToList().Count;
        var sprocCount = context.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking().ToList().Count;

        if (linqCount != sprocCount)
            throw new InvalidOperationException(
                $"Result sets differ: LINQ returned {linqCount} albums, sproc returned {sprocCount}.");
    }

    // ---------- No tracking (read-only) ----------

    [Benchmark(Baseline = true, Description = "LINQ")]
    [BenchmarkCategory("NoTracking")]
    public async Task<List<Album>> Linq_NoTracking()
    {
        await using var context = new ChinookContext(_options);
        return await context.Albums.AsNoTracking().ToListAsync();
    }

    [Benchmark(Description = "Sproc")]
    [BenchmarkCategory("NoTracking")]
    public async Task<List<Album>> Sproc_NoTracking()
    {
        await using var context = new ChinookContext(_options);
        return await context.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").AsNoTracking().ToListAsync();
    }

    // ---------- Tracking ----------

    [Benchmark(Baseline = true, Description = "LINQ")]
    [BenchmarkCategory("Tracking")]
    public async Task<List<Album>> Linq_Tracking()
    {
        await using var context = new ChinookContext(_options);
        return await context.Albums.ToListAsync();
    }

    [Benchmark(Description = "Sproc")]
    [BenchmarkCategory("Tracking")]
    public async Task<List<Album>> Sproc_Tracking()
    {
        await using var context = new ChinookContext(_options);
        return await context.Albums.FromSql($"EXEC dbo.sproc_GetAlbum").ToListAsync();
    }
}
