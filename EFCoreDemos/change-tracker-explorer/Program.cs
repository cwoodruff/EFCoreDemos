using change_tracker_explorer.Chinook;
using Microsoft.EntityFrameworkCore;

namespace change_tracker_explorer;

public static class Program
{
    public static void Main()
    {
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

        var newTrack = new Track
        {
            Name = "Bonus Track",
            AlbumId = album.Id,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "Demo",
            Milliseconds = 200_000,
            Bytes = 1_000_000,
            UnitPrice = 0.99m
        };
        db.Tracks.Add(newTrack);
        PrintTracker(db, "After Add new Track");

        db.SaveChanges();
        PrintTracker(db, "After Add + SaveChanges");

        tx.Rollback();
        Console.WriteLine();
        Console.WriteLine("(Transaction rolled back — the database is unchanged for the next run.)");
    }

    private static void PrintTracker(DbContext db, string label)
    {
        Console.WriteLine();
        Console.WriteLine($"== {label} ==");

        var grouped = db.ChangeTracker
            .Entries()
            .GroupBy(e => new { Type = e.Entity.GetType().Name, e.State })
            .OrderBy(g => g.Key.Type)
            .ThenBy(g => g.Key.State.ToString());

        foreach (var group in grouped)
        {
            var count = group.Count();
            var suffix = count > 1 ? $"  (x{count})" : string.Empty;
            Console.WriteLine($"{group.Key.Type,-10} : {group.Key.State}{suffix}");
        }
    }
}
