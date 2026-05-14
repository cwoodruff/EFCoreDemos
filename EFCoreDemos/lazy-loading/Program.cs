using System;
using System.Linq;
using lazy_loading.Chinook;
using Microsoft.EntityFrameworkCore;

namespace lazy_loading;

static class Program
{
    static void Main()
    {
        DemoLazyLoadingN1();
        Console.WriteLine();
        DemoChangeTrackerWalk();
    }

    // The classic lazy-loading N+1: load albums, then touch each album.Artist
    // inside the loop. Each property access fires a separate round-trip.
    private static void DemoLazyLoadingN1()
    {
        Console.WriteLine("== Lazy-loading N+1 (each artist access = one round-trip) ==");
        using var db = new ChinookContext();

        var albums = db.Albums.Take(5).ToList();
        foreach (var album in albums)
        {
            if (album.Artist != null)
                Console.WriteLine($"  {album.Title} -- {album.Artist.Name}");
        }

        Console.WriteLine("  (Look at the SQL log above: 1 SELECT for Albums + 1 SELECT per album.Artist access.)");
    }

    // The change tracker, made visible. Walk an Album+Tracks graph through
    // load / mutate / save / add / save, printing tracker state at each step.
    private static void DemoChangeTrackerWalk()
    {
        Console.WriteLine("== Change-tracker walk: Unchanged -> Modified -> Unchanged -> Added -> Unchanged ==");
        using var db = new ChinookContext();
        using var tx = db.Database.BeginTransaction();

        var album = db.Albums
            .Include(a => a.Tracks)
            .OrderBy(a => a.Id)
            .First(a => a.Tracks.Any());

        Console.WriteLine($"Loaded Album #{album.Id} '{album.Title}' with {album.Tracks.Count} Tracks.");
        PrintTracker(db, "After Load");

        album.Title = album.Title + " (renamed)";
        PrintTracker(db, "After Title mutation");

        db.SaveChanges();
        PrintTracker(db, "After SaveChanges");

        db.Tracks.Add(new Track
        {
            Name = "Bonus Track",
            AlbumId = album.Id,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "Demo",
            Milliseconds = 200_000,
            Bytes = 1_000_000,
            UnitPrice = 0.99m
        });
        PrintTracker(db, "After Add new Track");

        db.SaveChanges();
        PrintTracker(db, "After Add + SaveChanges");

        tx.Rollback();
        Console.WriteLine();
        Console.WriteLine("(Transaction rolled back -- the database is unchanged for the next run.)");
    }

    private static void PrintTracker(DbContext db, string label)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {label} --");

        var grouped = db.ChangeTracker
            .Entries()
            .GroupBy(e => new { Type = e.Metadata.ClrType.Name, e.State })
            .OrderBy(g => g.Key.Type)
            .ThenBy(g => g.Key.State.ToString());

        foreach (var group in grouped)
        {
            var count = group.Count();
            var suffix = count > 1 ? $"  (x{count})" : string.Empty;
            Console.WriteLine($"  {group.Key.Type,-10} : {group.Key.State}{suffix}");
        }
    }
}
