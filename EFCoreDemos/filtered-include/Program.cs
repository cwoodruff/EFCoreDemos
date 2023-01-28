using System;
using System.Linq;
using filtered_include.Chinook;
using Microsoft.EntityFrameworkCore;

namespace filtered_include;

public class Program
{
    static void Main()
    {
        using var db = new ChinookContext();

        var artists = db.Artists
            .Include(e => e.Albums.Where(a => a.Title.Contains("the")))
            .AsSplitQuery()
            .ToList();

        foreach (var artist in artists)
        {
            Console.WriteLine($"Found Album: {artist.Name} - {artist.Albums.Count}");
        }
    }
}