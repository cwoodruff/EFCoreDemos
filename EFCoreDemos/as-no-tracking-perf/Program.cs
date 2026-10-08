using as_no_tracking_perf.Chinook;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.EntityFrameworkCore;

namespace as_no_tracking_perf;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<TrackingBenchmarks>();
            return;
        }

        SilentBug.Run();

        Console.WriteLine();
        Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");
    }
}

[MemoryDiagnoser]
public class TrackingBenchmarks
{
    // Options without the console logger from OnConfiguring, so the benchmarks measure
    // the queries rather than writing every SQL command to the console.
    private static readonly DbContextOptions<ChinookContext> Options =
        new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlite("Data Source=chinook.db")
            .Options;

    // Each benchmark uses a fresh context, so tracked queries really start with an empty
    // change tracker instead of resolving against entities tracked by earlier invocations.
    [GlobalSetup]
    public void Setup()
    {
        using var db = new ChinookContext(Options);
        db.Tracks.AsNoTracking().FirstOrDefault();
    }

    [Benchmark(Baseline = true)]
    public List<Track> GetAllTracks_Tracked()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.ToList();
    }

    [Benchmark]
    public List<Track> GetAllTracks_NoTracking()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.AsNoTracking().ToList();
    }
}

public static class SilentBug
{
    public static void Run()
    {
        Console.WriteLine("== [silent-bug] AsNoTracking on a write path ==");

        using var db = new ChinookContext();

        var album = db.Albums.AsNoTracking().First();
        var originalTitle = album.Title;

        Console.WriteLine($"Loaded Album #{album.Id} ('{album.Title}') with AsNoTracking()");

        album.Title = originalTitle + " (edited)";
        var affected = db.SaveChanges();

        Console.WriteLine($"[silent-bug] SaveChanges returned: {affected}");
        Console.WriteLine("[silent-bug] Hint: add db.Update(album) before SaveChanges, or drop AsNoTracking.");

        var reread = db.Albums.AsNoTracking().First(a => a.Id == album.Id);
        Console.WriteLine($"[silent-bug] Title in DB after SaveChanges: '{reread.Title}' (unchanged).");
    }
}
