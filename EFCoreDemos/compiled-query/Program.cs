using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using compiled_query.Chinook;
using Microsoft.EntityFrameworkCore;

public class Program
{
    private static ChinookContext? _context;

    private static void Main()
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlServer(
            "Server=.;Database=Chinook;Trusted_Connection=True;TrustServerCertificate=True;Application Name=EFCoreDemos");

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
                // Create explicit compiled query
                var explicitQuery = EF.CompileQuery((ChinookContext context, int id)
                    => _context.Albums.FirstOrDefault(a => a.Id == id));

                List<Album?> l = new List<Album?>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled query
                    l.Add(explicitQuery(_context, id));
                }
            },
            name: "Compiled EF Core Query");

        RunTest(
            albumIDs =>
            {
                // Create explicit compiled query
                var compiledExplicitQuery = EF.CompileAsyncQuery((ChinookContext context, int id)
                    => context.Albums.FirstOrDefault(a => a.Id == id));
                
                List<Task> l = new List<Task>();
                foreach (var id in albumIDs)
                {
                    // Invoke the compiled async query
                    l.Add(compiledExplicitQuery(_context, id));
                }
                Task.WaitAny(l.ToArray());
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
                Task.WaitAny(l.ToArray());
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
        IQueryable<Album> albums = Queryable.Take(_context!.Albums, count);

        return albums.AsEnumerable().Select(i => i.Id).ToArray();
    }
}

[SimpleJob(RuntimeMoniker.Net70)]
public class CmpldQryBenchmark
{
    private int[] _albumIDs;
    private static ChinookContext? _context;

    [Params(500)]
    public int N;
    
    [GlobalSetup]
    public void Setup()
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlServer(
            "Server=.;Database=Chinook;Trusted_Connection=True;TrustServerCertificate=True;Application Name=EFCoreDemos");

        var dbContextOptions = builder.Options;
        _context = new ChinookContext(dbContextOptions);

        // Warm up
        _context.Artists.First();

        _albumIDs = GetAlbumIDs(N);
    }

    private static int[] GetAlbumIDs(int count)
    {
        IQueryable<Album> albums = Queryable.Take(_context!.Albums, count);

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
        // Create explicit compiled query
        var explicitQuery = EF.CompileQuery((ChinookContext context, int id)
            => _context.Albums.FirstOrDefault(a => a.Id == id));

        List<Album?> l = new List<Album?>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled query
            l.Add(explicitQuery(_context, id));
        }
    }
    
    [Benchmark]
    public void AsyncCompiledEFCoreQuery()
    {
        // Create explicit compiled query
        var compiledExplicitQuery = EF.CompileAsyncQuery((ChinookContext context, int id)
            => context.Albums.FirstOrDefault(a => a.Id == id));
                
        List<Task> l = new List<Task>();
        foreach (var id in _albumIDs)
        {
            // Invoke the compiled async query
            l.Add(compiledExplicitQuery(_context, id));
        }
        Task.WaitAny(l.ToArray());
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
        Task.WaitAny(l.ToArray());
    }
}