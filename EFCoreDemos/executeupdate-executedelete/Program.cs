// See https://aka.ms/new-console-template for more information

using executeupdate_executedelete.Chinook;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    public static async Task Main(string[] args)
    {
        using (var db = new ChinookContext())
        {
            await db.Tracks.TagWith("Updating tracks for genre Reggae by $.20")
                .Where(t => t.GenreId == 8)
                .ExecuteUpdateAsync(p => p.SetProperty(t => t.UnitPrice, t => t.UnitPrice + .20m));

            await db.Customers.TagWith("Deleting customers with invoices older than 2005")
                .Where(c => !c.Invoices.Any(i => i.InvoiceDate < new DateTime(2005, 1, 1))).ExecuteDeleteAsync();
        }

        Console.ReadLine();
    }
}