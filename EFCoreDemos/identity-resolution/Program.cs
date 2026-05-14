using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using identity_resolution.Chinook;
using Microsoft.EntityFrameworkCore;

namespace identity_resolution;

public class Program
{
    static void Main(string[] args)
    {
        DemoDuplicateKeyOnUpdate();

        if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<TrackingBenchmarks>();
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to compare");
        Console.WriteLine("      Tracked vs NoTracking vs NoTrackingWithIdentityResolution.");
    }

    private static void DemoDuplicateKeyOnUpdate()
    {
        Console.WriteLine("== Identity resolution: duplicate-key on Update ==");
        using var db = new ChinookContext();

        // A tracked load - the change tracker now has Album #1.
        var albumA = db.Albums.Single(e => e.Id == 1);
        var albumB = new Album { Id = 1, Title = "London Calling" };

        try
        {
            db.Update(albumB); // Same key as the already-tracked instance - this throws.
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.GetType().Name}: {e.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("== AsNoTrackingWithIdentityResolution preserves single-instance identity ==");
        using var db2 = new ChinookContext();

        // Without identity resolution, the same Artist row shows up as multiple .NET instances
        // across the loaded Album graph. With identity resolution, EF deduplicates them.
        var albumsPlain = db2.Albums.AsNoTracking().Include(a => a.Artist).Take(5).ToList();
        var distinctPlain = albumsPlain.Select(a => a.Artist).Distinct().Count();

        var albumsIdRes = db2.Albums.AsNoTrackingWithIdentityResolution().Include(a => a.Artist).Take(5).ToList();
        var distinctIdRes = albumsIdRes.Select(a => a.Artist).Distinct().Count();

        Console.WriteLine($"AsNoTracking()                          - distinct Artist instances across 5 Albums: {distinctPlain}");
        Console.WriteLine($"AsNoTrackingWithIdentityResolution()    - distinct Artist instances across 5 Albums: {distinctIdRes}");
    }
}

[MemoryDiagnoser]
public class TrackingBenchmarks
{
    private ChinookContext _db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _db = new ChinookContext();
        _db.Tracks.AsNoTracking().FirstOrDefault();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();

    // Baseline: every entity goes into the tracker.
    [Benchmark(Baseline = true)]
    public List<Track> Tracked() => _db.Tracks.ToList();

    // No tracker entry at all — fastest, but graphs can return duplicate instances for the same key.
    [Benchmark]
    public List<Track> NoTracking() => _db.Tracks.AsNoTracking().ToList();

    // No tracker entry, but a single instance per key — middle ground for read-only graphs.
    [Benchmark]
    public List<Track> NoTrackingWithIdentityResolution()
        => _db.Tracks.AsNoTrackingWithIdentityResolution().ToList();
}
