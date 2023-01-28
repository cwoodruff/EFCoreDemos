using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using query_tags.Chinook;

namespace query_tags;

public static class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            var invoices = db.Invoices
                .GroupBy(o => new { o.InvoiceDate })
                .Select(g => new
                {
                    g.Key.InvoiceDate,
                    Sum = g.Sum(o => o.Total),
                    Min = g.Min(o => o.Total),
                    Max = g.Max(o => o.Total),
                    Avg = g.Average(o => o.Total)
                })
                .TagWith("Description: Invoice Query from Query Tag demo")
                .TagWith("Query located: query_tags.Program.Main method")
                .TagWith(
                    @"Parameters:
                        None");

            foreach (var invoice in invoices)
            {
                Console.WriteLine("Sum " + invoice.Sum + " Invoice Date " + invoice.InvoiceDate);
            }
        }

        Console.ReadLine();
    }
}