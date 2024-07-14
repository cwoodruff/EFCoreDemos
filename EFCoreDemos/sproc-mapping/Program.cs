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
                .FromSql($"dbo.sproc_GetTrack")
                .ToList();
            
            Console.WriteLine(tracks.FirstOrDefault().Name);

            // Insert a new track
            
            // Update the track
            
            // Delete the track
            
        }

        Console.ReadLine();
    }
}