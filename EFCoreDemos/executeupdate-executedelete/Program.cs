using executeupdate_executedelete.Chinook;
using Microsoft.EntityFrameworkCore;

namespace executeupdate_executedelete;

internal class Program
{
    private const int ReggaeGenreId = 8;
    private const string Country = "Belgium";

    public static async Task Main(string[] args)
    {
        using (var db = new ChinookContext())
        {
            // Everything runs inside a transaction that is rolled back at the end, so the
            // SQL is real (and visible in the log) but chinook.db is unchanged after every run.
            await using var tx = await db.Database.BeginTransactionAsync();

            // ---- ExecuteUpdate: one UPDATE statement, zero entities loaded ----------------
            Console.WriteLine($"Reggae avg price before: {await AvgReggaePrice(db):0.00}");

            // NOTE: EF Core 10 drops TagWith() on ExecuteUpdate -- the tag below does not
            // appear in the generated UPDATE (verified on SQLite and SQL Server). Tags DO
            // work on ExecuteDelete (see below) and on regular queries.
            var updated = await db.Tracks.TagWith("Updating tracks for genre Reggae by $.20")
                .Where(t => t.GenreId == ReggaeGenreId)
                .ExecuteUpdateAsync(p => p.SetProperty(t => t.UnitPrice, t => t.UnitPrice + .20m));

            Console.WriteLine($"ExecuteUpdate changed {updated} rows. Reggae avg price now: {await AvgReggaePrice(db):0.00}");
            Console.WriteLine();

            // ---- ExecuteDelete: one DELETE statement per call -----------------------------
            // ExecuteDelete bypasses the change tracker, so EF does no client-side cascade.
            // The database's foreign keys still apply (Invoice -> Customer,
            // InvoiceLine -> Invoice), so we delete children first: lines, invoices, customers.
            var lines = await db.InvoiceLines.TagWith($"Deleting invoice lines for customers in {Country}")
                .Where(il => il.Invoice!.Customer!.Country == Country)
                .ExecuteDeleteAsync();

            var invoices = await db.Invoices.TagWith($"Deleting invoices for customers in {Country}")
                .Where(i => i.Customer!.Country == Country)
                .ExecuteDeleteAsync();

            var customers = await db.Customers.TagWith($"Deleting customers in {Country}")
                .Where(c => c.Country == Country)
                .ExecuteDeleteAsync();

            Console.WriteLine($"ExecuteDelete removed {lines} invoice lines, {invoices} invoices, {customers} customer(s) in {Country}.");

            await tx.RollbackAsync();
        }

        using (var db = new ChinookContext())
        {
            Console.WriteLine();
            Console.WriteLine("Transaction rolled back -- the database is unchanged for the next run:");
            Console.WriteLine($"  Reggae avg price: {await AvgReggaePrice(db):0.00}, customers in {Country}: {await db.Customers.CountAsync(c => c.Country == Country)}");
        }

        if (!Console.IsInputRedirected)
            Console.ReadLine();
    }

    private static Task<decimal> AvgReggaePrice(ChinookContext db) =>
        db.Tracks.Where(t => t.GenreId == ReggaeGenreId).AverageAsync(t => t.UnitPrice);
}
