using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using split_queries.Chinook;

namespace split_queries;

public class Program
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
            
        Console.WriteLine("--------------------------------------------------------");
            
        var invoices = db.Invoices
            .Include(e => e.InvoiceLines)
            .AsSingleQuery()
            .ToList();
            
        foreach (var invoice in invoices)
        {
            Console.WriteLine($"Found Invoice: {invoice.Id}");
        }
    }
}