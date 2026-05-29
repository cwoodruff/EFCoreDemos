using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using PaginationBenchmark.Data;

namespace PaginationBenchmark.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PaginationBenchmarks
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=PaginationBenchmark;Trusted_Connection=True;TrustServerCertificate=True;";

    private const int PageSize = 20;

    private AppDbContext _db = null!;
    private OffsetPaginationService _offset = null!;
    private KeysetPaginationService _keyset = null!;

    /// <summary>
    /// 1-based page number. With PageSize=20: page 1 = rows 1..20, page 50000 = rows 999981..1000000.
    /// Adjust to match how large your seeded table is.
    /// </summary>
    [Params(1, 100, 1_000, 10_000, 50_000)]
    public int PageNumber { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _db = new AppDbContext(options);
        _offset = new OffsetPaginationService(_db);
        _keyset = new KeysetPaginationService(_db);

        // Warm up the connection pool and EF Core's query compilation cache so the
        // first measured iteration isn't penalised by one-time setup costs.
        _ = _offset.GetPageAsync(1, PageSize).GetAwaiter().GetResult();
        _ = _keyset.GetPageAsync(null, PageSize).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<List<Order>> Offset()
        => await _offset.GetPageAsync(PageNumber, PageSize);

    [Benchmark]
    public async Task<List<Order>> Keyset()
    {
        // Keyset needs the cursor (last Id of the previous page). With our seeded
        // contiguous IDs this is deterministic: afterId = (PageNumber - 1) * PageSize.
        // In a real API the cursor comes from the previous response.
        int? afterId = PageNumber == 1 ? null : (PageNumber - 1) * PageSize;
        return await _keyset.GetPageAsync(afterId, PageSize);
    }
}
