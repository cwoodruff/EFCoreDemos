using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using required_1_on_1_dependants.Chinook;

namespace required_1_on_1_dependants;

class Program
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