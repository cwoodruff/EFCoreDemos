using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using simple_logging_improved_diagnostics.Chinook;

namespace simple_logging_improved_diagnostics;

class Program
{
    static void Main()
    {
        using var db = new ChinookContext();

        var artists = db.Artists
            .Include(e => e.Albums)
            .AsSplitQuery()
            .ToList();

        foreach (var artist in artists)
        {
            Console.WriteLine($"Found Artist: {artist.Name}");
        }
    }
}