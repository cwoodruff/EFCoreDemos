using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
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

    private static void Main()
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlite("Data Source=chinook.db");

        var dbContextOptions = builder.Options;
        _context = new ChinookContext(dbContextOptions);

        // Warm up
        _context.Artists.First();

        RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Use a regular auto-compiled query
                    l.Add(_context.Albums.FirstOrDefault(a => a.Id == id));
                }
            },
            name: "Run-time EF Core Query");

        RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled query
                    l.Add(ExplicitQuery(_context, id));
                }
            },
            name: "Compiled EF Core Query");

        RunTest(
            albumIDs =>
            {
                List<Task> l = new List<Task>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled async query
                    l.Add(CompiledExplicitQuery(_context, id));
                }
                Task.WaitAll(l.ToArray());
            },
            name: "Async Compiled EF Core Query");

        RunTest(
            albumIDs =>
            {
                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled query from DBContext
                    l.Add(_context.GetAlbum(id));
                }
            },
            name: "DBContext Compiled EF Core Query");

        RunTest(
            albumIDs =>
            {
                List<Task> l = new List<Task>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled async query from DBContext;
                    l.Add(_context.GetAlbumAsync(id));
                }
                Task.WaitAll(l.ToArray());
            },
            name: "DBContext Async Compiled EF Core Query");
        
        // dotnet run --project .\compiled-query.csproj -c Release
        //var summary = BenchmarkRunner.Run<CmpldQryBenchmark>();
    }
    
    private static void RunTest(Action<int[]> test, string name)
    {
        var albumIDs = GetAlbumIDs(500);
        var stopwatch = new Stopwatch();

        stopwatch.Start();

        test(albumIDs);

        stopwatch.Stop();

        Console.WriteLine($"{name}:  {stopwatch.ElapsedMilliseconds.ToString(),4}ms");
    }

    private static int[] GetAlbumIDs(int count)
    {
        IQueryable<Album> albums = Queryable.Take(_context.Albums, count);

        return albums.AsEnumerable().Select(i => i.Id).ToArray();
    }
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
        _context.Artists.First();

        _albumIDs = GetAlbumIDs(N);
    }

    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    private static int[] GetAlbumIDs(int count)
    {
        IQueryable<Album> albums = Queryable.Take(_context.Albums, count);

        return albums.AsEnumerable().Select(i => i.Id).ToArray();
    }

    [Benchmark]
    public void RunTimeEFCoreQuery()
    {
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
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled query
            l.Add(ExplicitQuery(_context, id));
        }
    }
    
    [Benchmark]
    public void AsyncCompiledEFCoreQuery()
    {
        List<Task> l = new List<Task>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled async query
            l.Add(CompiledExplicitQuery(_context, id));
        }
        Task.WaitAll(l.ToArray());
    }
    
    [Benchmark]
    public void DBContextCompiledEFCoreQuery()
    {
        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled query from DBContext
            l.Add(_context.GetAlbum(id));
        }
    }
    
    [Benchmark]
    public void DBContextAsyncCompiledEFCoreQuery()
    {
        List<Task> l = new List<Task>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled async query from DBContext;
            l.Add(_context.GetAlbumAsync(id));
        }
        Task.WaitAll(l.ToArray());
    }
}