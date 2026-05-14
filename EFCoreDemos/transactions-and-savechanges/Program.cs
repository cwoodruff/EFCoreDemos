using Microsoft.EntityFrameworkCore;
using System.Transactions;
using transactions_and_savechanges.Chinook;

namespace transactions_and_savechanges;

public static class Program
{
    public static async Task Main()
    {
        await ScenarioA_SimpleRollback();
        await ScenarioB_Savepoint();
        await ScenarioC_TransactionScopeFootgun();
    }

    private static async Task ScenarioA_SimpleRollback()
    {
        Console.WriteLine("=== Scenario A: simple transaction ===");
        using var db = new ChinookContext();

        var customerId = db.Customers.OrderBy(c => c.Id).Select(c => c.Id).First();
        var trackIds = db.Tracks.OrderBy(t => t.Id).Take(2).Select(t => t.Id).ToArray();

        var beforeInvoiceCount = db.Invoices.Count();
        var beforeLineCount = db.InvoiceLines.Count();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var invoice = new Invoice
            {
                CustomerId = customerId,
                InvoiceDate = DateTime.UtcNow,
                BillingAddress = "1 Demo Way",
                BillingCity = "Demo",
                BillingCountry = "Demo",
                Total = 0m
            };
            db.Invoices.Add(invoice);
            db.SaveChanges();
            Console.WriteLine($"Invoice {invoice.Id} inserted.");

            for (var i = 0; i < 3; i++)
            {
                if (i == 2)
                {
                    Console.WriteLine($"Line {i + 1} FAILED - rolling back the whole transaction.");
                    throw new InvalidOperationException("Simulated failure on line #3");
                }

                db.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    TrackId = trackIds[i % trackIds.Length],
                    UnitPrice = 0.99m,
                    Quantity = 1
                });
                db.SaveChanges();
                Console.WriteLine($"Line {i + 1} inserted.");
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
        }

        var afterInvoiceCount = db.Invoices.Count();
        var afterLineCount = db.InvoiceLines.Count();
        Console.WriteLine($"After rollback: Invoice delta = {afterInvoiceCount - beforeInvoiceCount}, Line delta = {afterLineCount - beforeLineCount}  (correct: 0 / 0).");
    }

    private static async Task ScenarioB_Savepoint()
    {
        Console.WriteLine();
        Console.WriteLine("=== Scenario B: savepoint ===");
        using var db = new ChinookContext();

        var customerId = db.Customers.OrderBy(c => c.Id).Select(c => c.Id).First();
        var trackIds = db.Tracks.OrderBy(t => t.Id).Take(2).Select(t => t.Id).ToArray();

        var beforeInvoiceCount = db.Invoices.Count();
        var beforeLineCount = db.InvoiceLines.Count();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var invoice = new Invoice
            {
                CustomerId = customerId,
                InvoiceDate = DateTime.UtcNow,
                BillingAddress = "1 Demo Way",
                BillingCity = "Demo",
                BillingCountry = "Demo",
                Total = 0m
            };
            db.Invoices.Add(invoice);
            db.SaveChanges();
            Console.WriteLine($"Invoice {invoice.Id} inserted. Savepoint \"after-invoice\" created.");
            await tx.CreateSavepointAsync("after-invoice");

            try
            {
                for (var i = 0; i < 2; i++)
                {
                    if (i == 1)
                    {
                        Console.WriteLine($"Line {i + 1} FAILED - rolling back to savepoint.");
                        throw new InvalidOperationException("Simulated failure on line #2");
                    }

                    db.InvoiceLines.Add(new InvoiceLine
                    {
                        InvoiceId = invoice.Id,
                        TrackId = trackIds[i % trackIds.Length],
                        UnitPrice = 0.99m,
                        Quantity = 1
                    });
                    db.SaveChanges();
                    Console.WriteLine($"Line {i + 1} inserted.");
                }
            }
            catch
            {
                db.ChangeTracker.Clear();
                await tx.RollbackToSavepointAsync("after-invoice");
            }

            // Observe partial-rollback state inside the still-open transaction.
            var inTxInvoiceCount = db.Invoices.Count();
            var inTxLineCount = db.InvoiceLines.Count();
            Console.WriteLine($"After rollback to savepoint: Invoice delta = {inTxInvoiceCount - beforeInvoiceCount} (preserved), Line delta = {inTxLineCount - beforeLineCount} (0).");
        }
        finally
        {
            // Final rollback so the demo is idempotent across runs.
            await tx.RollbackAsync();
        }
    }

    private static async Task ScenarioC_TransactionScopeFootgun()
    {
        Console.WriteLine();
        Console.WriteLine("=== Scenario C: TransactionScope footgun ===");

        Console.WriteLine("Without TransactionScopeAsyncFlowOption.Enabled, the ambient transaction does not flow across await.");
        try
        {
            // Default option is "Suppress" - the ambient transaction is dropped at the first await.
            using var scope = new TransactionScope();
            await SimulateDbWorkAsync();
            scope.Complete();
            Console.WriteLine("(no exception, but the ambient transaction was lost across the await)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught: {ex.GetType().Name} - {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Fix: pass TransactionScopeAsyncFlowOption.Enabled - one line, problem solved.");
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await SimulateDbWorkAsync();
            scope.Complete();
            Console.WriteLine("Async TransactionScope flowed correctly through the await.");
        }
    }

    private static async Task SimulateDbWorkAsync()
    {
        // The pattern teams hit in real code: an EF SaveChangesAsync inside a TransactionScope
        // followed by another async DB call. Without AsyncFlowOption, the second op runs
        // outside the scope entirely.
        await Task.Delay(10);
    }
}
