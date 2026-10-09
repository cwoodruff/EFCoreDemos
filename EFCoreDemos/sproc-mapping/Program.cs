using Microsoft.EntityFrameworkCore;
using sproc_mapping.Chinook;

#pragma warning disable 169

namespace sproc_mapping;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            // Get all the tracks in the database
            var tracks = db.Tracks
                .FromSql($"EXEC dbo.sproc_GetTrack")
                .ToList();
            
            Console.WriteLine(tracks.FirstOrDefault()?.Name);

            // Insert a new track (SaveChanges calls dbo.sproc_InsertTrack; the new Id comes back as an OUTPUT parameter)
            var track = new Track
            {
                Name = "Sproc Mapping Demo",
                AlbumId = 1,
                MediaTypeId = 1,
                GenreId = 1,
                Composer = "EF Core",
                Milliseconds = 180000,
                Bytes = 1024,
                UnitPrice = 0.99m
            };
            db.Tracks.Add(track);
            db.SaveChanges();
            Console.WriteLine($"Inserted track {track.Id}: {track.Name}");

            // Update the track (SaveChanges calls dbo.sproc_UpdateTrack)
            track.Name = "Sproc Mapping Demo (updated)";
            db.SaveChanges();
            Console.WriteLine($"Updated track {track.Id}: {track.Name}");

            // Delete the track (SaveChanges calls dbo.sproc_DeleteTrack)
            db.Tracks.Remove(track);
            db.SaveChanges();
            Console.WriteLine($"Deleted track {track.Id}");
        }

        Console.ReadLine();
    }
}