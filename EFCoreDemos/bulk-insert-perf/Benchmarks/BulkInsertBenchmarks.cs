using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using bulk_insert_perf.Data;
using Microsoft.EntityFrameworkCore;

namespace bulk_insert_perf.Benchmarks;

/// <summary>
/// Inserts RowCount order lines into an empty table, five ways. The table is truncated and fresh entities are
/// generated before every iteration (outside the timed region), so each measurement is one cold import.
///
/// Per-row SaveChanges only runs at the smallest size: at 100k rows it takes minutes per iteration.
/// </summary>
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 5)]
[MemoryDiagnoser]
public class BulkInsertBenchmarks
{
    public const string ConnectionString =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;Database=BulkInsertDemo;TrustServerCertificate=True;Application Name=EFCoreDemos-BulkInsertPerf;";

    public const int PerRowMaxRows = 1_000;

    [Params(1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    private DbContextOptions<BulkContext> _defaultOptions = null!;
    private DbContextOptions<BulkContext> _tunedOptions = null!;
    private List<OrderLine> _rows = null!;

    /// <summary>Skips the per-row benchmark above <see cref="PerRowMaxRows"/> rows.</summary>
    public static IConfig CreateConfig() => DefaultConfig.Instance.AddFilter(new SimpleFilter(benchmark =>
        benchmark.Descriptor.WorkloadMethod.Name != nameof(PerRowSaveChanges)
        || (int)benchmark.Parameters["RowCount"] <= PerRowMaxRows));

    [GlobalSetup]
    public void Setup()
    {
        // No logger or interceptor: they would add their own cost to every command.
        _defaultOptions = BulkContext.CreateOptions(ConnectionString);
        _tunedOptions = BulkContext.CreateOptions(ConnectionString, BulkInserts.TunedBatchSize);
        BulkContext.ResetAsync(ConnectionString).GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        BulkContext.TruncateAsync(ConnectionString).GetAwaiter().GetResult();
        _rows = OrderLine.Generate(RowCount);
    }

    [GlobalCleanup]
    public void Cleanup() => BulkContext.TruncateAsync(ConnectionString).GetAwaiter().GetResult();

    [Benchmark(Description = "Add + SaveChanges per row")]
    public Task PerRowSaveChanges() => BulkInserts.PerRowSaveChangesAsync(_defaultOptions, _rows);

    [Benchmark(Baseline = true, Description = "AddRange (default batch)")]
    public Task AddRange() => BulkInserts.AddRangeAsync(_defaultOptions, _rows);

    [Benchmark(Description = "AddRange + MaxBatchSize(400)")]
    public Task AddRangeTunedBatch() => BulkInserts.AddRangeAsync(_tunedOptions, _rows);

    [Benchmark(Description = "AutoDetectChanges off")]
    public Task AddWithoutDetectChanges() => BulkInserts.AddWithoutDetectChangesAsync(_defaultOptions, _rows);

    [Benchmark(Description = "SqlBulkCopy")]
    public Task SqlBulkCopy() => BulkInserts.SqlBulkCopyAsync(ConnectionString, _rows);
}
