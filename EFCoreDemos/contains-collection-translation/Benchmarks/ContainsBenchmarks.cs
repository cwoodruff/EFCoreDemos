using BenchmarkDotNet.Attributes;
using contains_collection_translation.Chinook;
using Microsoft.EntityFrameworkCore;

namespace contains_collection_translation.Benchmarks;

/// <summary>
/// Times <c>Where(t => ids.Contains(t.Id))</c> under each translation strategy for lists of 5, 50 and 500 ids.
///
/// Every invocation draws a fresh random list, as real traffic would (search filters, selected rows).
/// That is the case that hurts EF.Constant: each new list is a new SQL text, so SQL Server compiles a
/// new plan every time. The parameterized strategies reuse their cached plans.
/// </summary>
[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
public class ContainsBenchmarks
{
    public const string ConnectionString =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;Database=Chinook;TrustServerCertificate=True;Application Name=EFCoreDemos-ContainsTranslation;";

    // Constant run id in the TagWith comment; GlobalCleanup evicts exactly these plans.
    private const string Run = "benchmark";

    private DbContextOptions<ChinookContext> _options = null!;
    private Random _random = null!;

    [Params(5, 50, 500)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // No logger factory: console logging would dominate the measurements.
        _options = new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _random = new Random(42);

        // Warm up the model and connection pool, and check all strategies return the same rows.
        using var context = new ChinookContext(_options);
        var ids = TrackQueries.RandomIds(_random, Size);
        var expected = ids.ToList();
        foreach (var strategy in Enum.GetValues<Strategy>())
        {
            var actual = TrackQueries.ByIds(context, ids, strategy, Run).Select(t => t.Id).OrderBy(id => id).ToList();
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"{strategy} returned {actual.Count} rows, expected {expected.Count}.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() =>
        PlanCache.EvictAsync(ConnectionString, $"{TrackQueries.TagPrefix} {Run}").GetAwaiter().GetResult();

    [Benchmark(Baseline = true, Description = "Default (MultipleParameters)")]
    public Task<List<Track>> Default() => Query(Strategy.Default);

    [Benchmark(Description = "EF.Constant")]
    public Task<List<Track>> Constant() => Query(Strategy.Constant);

    [Benchmark(Description = "EF.Parameter (OPENJSON)")]
    public Task<List<Track>> Parameter() => Query(Strategy.Parameter);

    private async Task<List<Track>> Query(Strategy strategy)
    {
        var ids = TrackQueries.RandomIds(_random, Size);
        await using var context = new ChinookContext(_options);
        return await TrackQueries.ByIds(context, ids, strategy, Run).ToListAsync();
    }
}
