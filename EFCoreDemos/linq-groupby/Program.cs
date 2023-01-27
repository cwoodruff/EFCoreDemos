using System;
using System.Linq;
using linq_groupby.Chinook;

namespace linq_groupby;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            var invoices = db.Invoices
                .GroupBy(o => new {o.InvoiceDate})
                .Select(g => new
                {
                    g.Key.InvoiceDate,
                    Sum = g.Sum(o => o.Total),
                    Min = g.Min(o => o.Total),
                    Max = g.Max(o => o.Total),
                    Avg = g.Average(o => o.Total)
                });

            foreach (var invoice in invoices)
            {
                Console.WriteLine("Sum " + invoice.Sum + " Invoice Date " + invoice.InvoiceDate);
            }
        }

        Console.ReadLine();
    }
}