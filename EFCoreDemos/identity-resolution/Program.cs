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
        // Albums 1-5 by Id belong to only 3 artists (AC/DC, Accept, Aerosmith).
        var albumsPlain = db2.Albums.AsNoTracking().Include(a => a.Artist).OrderBy(a => a.Id).Take(5).ToList();
        var distinctPlain = albumsPlain.Select(a => a.Artist).Distinct().Count();

        var albumsIdRes = db2.Albums.AsNoTrackingWithIdentityResolution().Include(a => a.Artist).OrderBy(a => a.Id).Take(5).ToList();
        var distinctIdRes = albumsIdRes.Select(a => a.Artist).Distinct().Count();

        Console.WriteLine($"AsNoTracking()                          - distinct Artist instances across 5 Albums: {distinctPlain}");
        Console.WriteLine($"AsNoTrackingWithIdentityResolution()    - distinct Artist instances across 5 Albums: {distinctIdRes}");
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

    // Baseline: every entity goes into the tracker.
    [Benchmark(Baseline = true)]
    public List<Track> Tracked()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.ToList();
    }

    // No tracker entry at all — fastest, but graphs can return duplicate instances for the same key.
    [Benchmark]
    public List<Track> NoTracking()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.AsNoTracking().ToList();
    }

    // No tracker entry, but a single instance per key — middle ground for read-only graphs.
    [Benchmark]
    public List<Track> NoTrackingWithIdentityResolution()
    {
        using var db = new ChinookContext(Options);
        return db.Tracks.AsNoTrackingWithIdentityResolution().ToList();
    }
}
