using Microsoft.EntityFrameworkCore;
using n_plus_one_problem.Chinook;

namespace n_plus_one_problem;

public static class Program
{
    public static void Main()
    {
        SectionA_TheBug();
        SectionB_Include();
        SectionC_Project();
        SectionD_BatchedIn();
    }

    private static int _executed;

    private static IDisposable Counting()
    {
        _executed = 0;
        ChinookContext.LogSink = line =>
        {
            if (line.Contains("Executed DbCommand", StringComparison.Ordinal))
            {
                _executed++;
            }
            Console.WriteLine(line);
        };
        return new DisposableAction(() => Console.WriteLine($"Total SQL statements: {_executed}"));
    }

    private static void SectionA_TheBug()
    {
        Console.WriteLine("=== Section A: The N+1 bug ===");
        using var _ = Counting();
        using var db = new ChinookContext();

        var customers = db.Customers.ToList();
        decimal grandTotal = 0m;
        foreach (var c in customers)
        {
            grandTotal += db.Invoices.Where(i => i.CustomerId == c.Id).Sum(i => i.Total);
        }
        Console.WriteLine($"(Customers scanned: {customers.Count}, grandTotal={grandTotal:F2})");
    }

    private static void SectionB_Include()
    {
        Console.WriteLine();
        Console.WriteLine("=== Section B: Fix 1 - Include ===");
        using var _ = Counting();
        using var db = new ChinookContext();

        var customers = db.Customers.Include(c => c.Invoices).ToList();
        var grandTotal = customers.Sum(c => c.Invoices.Sum(i => i.Total));
        Console.WriteLine($"(Customers scanned: {customers.Count}, grandTotal={grandTotal:F2})");
    }

    private static void SectionC_Project()
    {
        Console.WriteLine();
        Console.WriteLine("=== Section C: Fix 2 - Project ===");
        using var _ = Counting();
        using var db = new ChinookContext();

        var rows = db.Customers
            .Select(c => new
            {
                c.Id,
                Name = c.FirstName + " " + c.LastName,
                InvoiceTotal = c.Invoices.Sum(i => (decimal?)i.Total) ?? 0m
            })
            .ToList();

        var grandTotal = rows.Sum(r => r.InvoiceTotal);
        Console.WriteLine($"(Customers scanned: {rows.Count}, grandTotal={grandTotal:F2})");
    }

    private static void SectionD_BatchedIn()
    {
        Console.WriteLine();
        Console.WriteLine("=== Section D: Fix 3 - Batched IN ===");
        using var _ = Counting();
        using var db = new ChinookContext();

        var customers = db.Customers.AsNoTracking().ToList();
        var ids = customers.Select(c => c.Id).ToArray();

        var invoiceByCustomer = db.Invoices
            .Where(i => ids.Contains(i.CustomerId))
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Total) })
            .ToLookup(x => x.CustomerId, x => x.Total);

        decimal grandTotal = 0m;
        foreach (var c in customers)
        {
            grandTotal += invoiceByCustomer[c.Id].FirstOrDefault();
        }
        Console.WriteLine($"(Customers scanned: {customers.Count}, grandTotal={grandTotal:F2})");
    }

    private sealed class DisposableAction(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
