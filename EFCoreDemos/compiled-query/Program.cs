using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using compiled_query.Chinook;
using Microsoft.EntityFrameworkCore;

namespace compiled_query;

public class Program
{
    private static ChinookContext _context = null!;

    // Create explicit compiled queries once, so their compilation cost isn't part of the timed tests
    private static readonly Func<ChinookContext, int, Album?> ExplicitQuery =
        EF.CompileQuery((ChinookContext context, int id)
            => context.Albums.FirstOrDefault(a => a.Id == id));

    private static readonly Func<ChinookContext, int, Task<Album?>> CompiledExplicitQuery =
        EF.CompileAsyncQuery((ChinookContext context, int id)
            => context.Albums.FirstOrDefault(a => a.Id == id));

    // Quick demo:            dotnet run -c Release
    // Full BenchmarkDotNet:  dotnet run -c Release -- benchmark
    private static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0].TrimStart('-').Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<CmpldQryBenchmark>();
            return;
        }

        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlite("Data Source=chinook.db");

        var dbContextOptions = builder.Options;
        _context = new ChinookContext(dbContextOptions);

        // Warm up every variant once (JIT, query cache, materializer) so the first timed test
        // doesn't pay one-time costs the others don't.
        _context.Albums.FirstOrDefault(a => a.Id == 1);
        ExplicitQuery(_context, 1);
        await CompiledExplicitQuery(_context, 1);
        _context.GetAlbum(1);
        await _context.GetAlbumAsync(1);

        await RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Use a regular auto-compiled query
                    l.Add(_context.Albums.FirstOrDefault(a => a.Id == id));
                }
                return Task.CompletedTask;
            },
            name: "Run-time EF Core Query");

        await RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled query
                    l.Add(ExplicitQuery(_context, id));
                }
                return Task.CompletedTask;
            },
            name: "Compiled EF Core Query");

        await RunTest(
            async albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled async query. A DbContext does not support concurrent
                    // operations, so each query is awaited before the next one starts.
                    l.Add(await CompiledExplicitQuery(_context, id));
                }
            },
            name: "Async Compiled EF Core Query");

        await RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled query from DBContext
                    l.Add(_context.GetAlbum(id));
                }
                return Task.CompletedTask;
            },
            name: "DBContext Compiled EF Core Query");

        await RunTest(
            async albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled async query from DBContext (awaited sequentially).
                    l.Add(await _context.GetAlbumAsync(id));
                }
            },
            name: "DBContext Async Compiled EF Core Query");
    }

    private static async Task RunTest(Func<int[], Task> test, string name)
    {
        var albumIDs = GetAlbumIDs(500);

        // Every test starts with an empty change tracker, so each one materializes and tracks
        // the same albums. Without this, later tests would just find already-tracked instances.
        _context.ChangeTracker.Clear();

        var stopwatch = Stopwatch.StartNew();

        await test(albumIDs);

        stopwatch.Stop();

        Console.WriteLine($"{name,-40} {stopwatch.ElapsedMilliseconds,4}ms  ({albumIDs.Length} queries)");
    }

    // Projects only the keys, so fetching the IDs doesn't put any albums into the change tracker.
    private static int[] GetAlbumIDs(int count) =>
        _context.Albums.OrderBy(a => a.Id).Take(count).Select(a => a.Id).ToArray();
}

[SimpleJob(RuntimeMoniker.Net10_0)]
public class CmpldQryBenchmark
{
    private int[] _albumIDs = [];
    private static ChinookContext _context = null!;

    // Create explicit compiled queries once, so their compilation cost isn't part of the benchmarks
    private static readonly Func<ChinookContext, int, Album?> ExplicitQuery =
        EF.CompileQuery((ChinookContext context, int id)
            => context.Albums.FirstOrDefault(a => a.Id == id));

    private static readonly Func<ChinookContext, int, Task<Album?>> CompiledExplicitQuery =
        EF.CompileAsyncQuery((ChinookContext context, int id)
            => context.Albums.FirstOrDefault(a => a.Id == id));

    [Params(500)]
    public int N;
    
    [GlobalSetup]
    public void Setup()
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlite("Data Source=chinook.db");

        var dbContextOptions = builder.Options;
        _context = new ChinookContext(dbContextOptions);

        // Warm up
        _context.Artists.AsNoTracking().First();

        _albumIDs = GetAlbumIDs(N);
    }

    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    private static int[] GetAlbumIDs(int count) =>
        _context.Albums.OrderBy(a => a.Id).Take(count).Select(a => a.Id).ToArray();

    // Every benchmark clears the change tracker first (same cost for all of them), so each
    // invocation really materializes and tracks the albums instead of hitting tracked instances.
    [Benchmark]
    public void RunTimeEFCoreQuery()
    {
        _context.ChangeTracker.Clear();
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Use a regular auto-compiled query
            l.Add(_context.Albums.FirstOrDefault(a => a.Id == id));
        }
    }
    
    [Benchmark]
    public void CompiledEFCoreQuery()
    {
        _context.ChangeTracker.Clear();
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled query
            l.Add(ExplicitQuery(_context, id));
        }
    }
    
    [Benchmark]
    public async Task AsyncCompiledEFCoreQuery()
    {
        _context.ChangeTracker.Clear();
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled async query (awaited sequentially - one DbContext, one operation at a time)
            l.Add(await CompiledExplicitQuery(_context, id));
        }
    }
    
    [Benchmark]
    public void DBContextCompiledEFCoreQuery()
    {
        _context.ChangeTracker.Clear();
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled query from DBContext
            l.Add(_context.GetAlbum(id));
        }
    }
    
    [Benchmark]
    public async Task DBContextAsyncCompiledEFCoreQuery()
    {
        _context.ChangeTracker.Clear();
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled async query from DBContext (awaited sequentially)
            l.Add(await _context.GetAlbumAsync(id));
        }
    }
}