using System;
using System.Linq;
using Demos.Chinook;
using Microsoft.EntityFrameworkCore;

namespace Demos;

public class Program
{
    // Jane Peacock: the support rep for 21 of the 59 Chinook customers.
    private const int SalesRepId = 3;

    private static void Main()
    {
        // No command logging: each step prints its SQL with ToQueryString() instead.
        var options = new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlite("Data Source=chinook.db")
            .Options;

        using var db = new ChinookContext(options) { SalesRepId = SalesRepId };

        var rep = db.Employees.Single(e => e.Id == SalesRepId);
        var invoiceType = db.Model.FindEntityType(typeof(Invoice))!;

        Console.WriteLine("EF Core 10 named query filters on Invoice:");
        foreach (var filter in invoiceType.GetDeclaredQueryFilters())
        {
            Console.WriteLine($"  [{filter.Key}] {filter.Expression}");
        }
        Console.WriteLine($"SalesRepId = {SalesRepId} ({rep.FirstName} {rep.LastName})");
        Console.WriteLine();

        Show("1. Both filters on (default)",
            db.Invoices);

        Show($"2. IgnoreQueryFilters([\"{ChinookContext.Since2009Filter}\"]) - every year, still one rep",
            db.Invoices.IgnoreQueryFilters([ChinookContext.Since2009Filter]));

        Show($"3. IgnoreQueryFilters([\"{ChinookContext.SalesRepFilter}\"]) - every rep, still 2009 onwards",
            db.Invoices.IgnoreQueryFilters([ChinookContext.SalesRepFilter]));

        Show("4. IgnoreQueryFilters() - all filters off",
            db.Invoices.IgnoreQueryFilters());

        // Filters also apply to navigations loaded with Include.
        var customers = db.Customers.Include(c => c.Invoices).ToList();
        Console.WriteLine("5. Customers.Include(c => c.Invoices) - filters apply to the included invoices too");
        Console.WriteLine($"-> {customers.Count} customers (Customer itself is unfiltered), " +
                          $"{customers.Sum(c => c.Invoices.Count)} invoices, " +
                          $"{customers.Count(c => c.Invoices.Count > 0)} customers with at least one");
        Console.WriteLine();

        Console.WriteLine("Before EF Core 10: one filter per entity. Calling HasQueryFilter a second time");
        Console.WriteLine("silently replaced the first, so both conditions had to be &&-ed into one lambda,");
        Console.WriteLine("and IgnoreQueryFilters() could only switch that whole lambda off - steps 2 and 3");
        Console.WriteLine("were impossible without a second DbContext or hand-written predicates.");
    }

    private static void Show(string title, IQueryable<Invoice> query)
    {
        Console.WriteLine($"== {title} ==");
        Console.WriteLine(query.ToQueryString());
        Console.WriteLine($"-> {query.Count()} invoices");
        Console.WriteLine();
    }
}
