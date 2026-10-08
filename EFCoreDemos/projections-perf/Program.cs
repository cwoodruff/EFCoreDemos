using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.EntityFrameworkCore;
using projections_perf.Chinook;

namespace projections_perf;

public static class Program
{
    public static void Main(string[] args)
    {
        WriteSqlSnapshot();

        if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<ProjectionBenchmarks>();
            return;
        }

        Console.WriteLine("SQL snapshots written to ./sql/*.sql");
        Console.WriteLine("Run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");
    }

    private static void WriteSqlSnapshot()
    {
        using var db = new ChinookContext();

        Directory.CreateDirectory("sql");

        var fullSql = db.Tracks.Where(t => t.UnitPrice >= 0.99m).ToQueryString();
        File.WriteAllText(Path.Combine("sql", "full-hydration.sql"), fullSql);

        var anonSql = db.Tracks
            .Where(t => t.UnitPrice >= 0.99m)
            .Select(t => new { t.Id, t.Name, t.AlbumId })
            .ToQueryString();
        File.WriteAllText(Path.Combine("sql", "anon-projection.sql"), anonSql);

        var dtoSql = db.Tracks
            .Where(t => t.UnitPrice >= 0.99m)
            .Select(t => new TrackListItem
            {
                Id = t.Id,
                Name = t.Name,
                AlbumTitle = t.Album.Title
            })
            .ToQueryString();
        File.WriteAllText(Path.Combine("sql", "dto-projection.sql"), dtoSql);
    }
}

public class TrackListItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string AlbumTitle { get; set; }
}

[MemoryDiagnoser]
public class ProjectionBenchmarks
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
    public List<Track> FullHydration()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.Where(t => t.UnitPrice >= 0.99m).ToList();
    }

    [Benchmark]
    public object AnonProjection()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks
            .Where(t => t.UnitPrice >= 0.99m)
            .Select(t => new { t.Id, t.Name, t.AlbumId })
            .ToList();
    }

    [Benchmark]
    public List<TrackListItem> DtoProjection()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks
            .Where(t => t.UnitPrice >= 0.99m)
            .Select(t => new TrackListItem
            {
                Id = t.Id,
                Name = t.Name,
                AlbumTitle = t.Album.Title
            })
            .ToList();
    }
}
