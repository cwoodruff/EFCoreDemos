extern alias executeupdate_executedelete;

using System.Data.Common;
using EFCoreDemos.Tests.Infrastructure;
using executeupdate_executedelete::executeupdate_executedelete.Chinook;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// executeupdate-executedelete: ExecuteUpdate / ExecuteDelete run one set-based UPDATE / DELETE statement,
    /// return the affected row count, never load entities and bypass the change tracker (no client-side cascade,
    /// tracked entities go stale). The database's foreign keys still apply.
    /// </summary>
    public class ExecuteUpdateExecuteDeleteTests : IDisposable
    {
        private const int ReggaeGenreId = 8;
        private const int ReggaeTrackCount = 58;
        private const decimal ReggaeOriginalPrice = 0.99m;
        private const string Country = "Belgium";
        private const int BelgiumInvoiceLines = 42;
        private const int BelgiumInvoices = 7;
        private const int BelgiumCustomers = 1;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        [Fact]
        public async Task ExecuteUpdate_IssuesOneUpdate_ReturnsRowCount_LoadsNothing()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var updated = await db.Tracks
                .Where(t => t.GenreId == ReggaeGenreId)
                .ExecuteUpdateAsync(p => p.SetProperty(t => t.UnitPrice, t => t.UnitPrice + .20m));

            Assert.Equal(ReggaeTrackCount, updated);
            var command = Assert.Single(log.Commands);
            Assert.Contains("UPDATE \"Track\"", command);
            Assert.DoesNotContain("SELECT", command, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(db.ChangeTracker.Entries());

            using var verify = CreateContext();
            var prices = await verify.Tracks.Where(t => t.GenreId == ReggaeGenreId).Select(t => t.UnitPrice).ToListAsync();
            Assert.Equal(ReggaeTrackCount, prices.Count);
            Assert.All(prices, p => Assert.Equal(ReggaeOriginalPrice + .20m, Math.Round(p, 2)));
        }

        [Fact]
        public async Task ExecuteUpdate_BypassesChangeTracker_TrackedEntityStaysStale()
        {
            using var db = CreateContext();
            var tracked = await db.Tracks.FirstAsync(t => t.GenreId == ReggaeGenreId);
            Assert.Equal(ReggaeOriginalPrice, Math.Round(tracked.UnitPrice, 2));

            await db.Tracks
                .Where(t => t.GenreId == ReggaeGenreId)
                .ExecuteUpdateAsync(p => p.SetProperty(t => t.UnitPrice, t => t.UnitPrice + .20m));

            // The tracked instance and its entry know nothing about the UPDATE.
            Assert.Equal(ReggaeOriginalPrice, Math.Round(tracked.UnitPrice, 2));
            Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);

            // Re-querying a tracked entity keeps the (stale) tracked values (identity resolution)...
            var requeried = await db.Tracks.FirstAsync(t => t.Id == tracked.Id);
            Assert.Same(tracked, requeried);
            Assert.Equal(ReggaeOriginalPrice, Math.Round(requeried.UnitPrice, 2));

            // ...until it is explicitly reloaded.
            await db.Entry(tracked).ReloadAsync();
            Assert.Equal(ReggaeOriginalPrice + .20m, Math.Round(tracked.UnitPrice, 2));
        }

        [Fact]
        public async Task ExecuteDelete_ChildrenFirst_OneDeletePerCall_WithTags()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var lines = await db.InvoiceLines.TagWith($"Deleting invoice lines for customers in {Country}")
                .Where(il => il.Invoice!.Customer!.Country == Country)
                .ExecuteDeleteAsync();
            var invoices = await db.Invoices.TagWith($"Deleting invoices for customers in {Country}")
                .Where(i => i.Customer!.Country == Country)
                .ExecuteDeleteAsync();
            var customers = await db.Customers.TagWith($"Deleting customers in {Country}")
                .Where(c => c.Country == Country)
                .ExecuteDeleteAsync();

            Assert.Equal(BelgiumInvoiceLines, lines);
            Assert.Equal(BelgiumInvoices, invoices);
            Assert.Equal(BelgiumCustomers, customers);

            var commands = log.Commands;
            Assert.Equal(3, commands.Count);
            Assert.All(commands, c => Assert.Contains("DELETE FROM", c));
            Assert.Contains("-- Deleting invoice lines for customers in Belgium", commands[0]);
            Assert.Contains("-- Deleting customers in Belgium", commands[2]);
            Assert.Empty(db.ChangeTracker.Entries());

            using var verify = CreateContext();
            Assert.Equal(0, await verify.Customers.CountAsync(c => c.Country == Country));
            Assert.Equal(0, await verify.Invoices.CountAsync(i => i.Customer!.Country == Country));
        }

        [Fact]
        public async Task ExecuteDelete_DoesNoClientSideCascade_DatabaseForeignKeyRejectsParentFirst()
        {
            using var db = CreateContext();

            var ex = await Assert.ThrowsAnyAsync<DbException>(() =>
                db.Customers.Where(c => c.Country == Country).ExecuteDeleteAsync());

            Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(BelgiumCustomers, await db.Customers.CountAsync(c => c.Country == Country));
        }

        [Fact]
        public async Task ExecuteUpdateAndDelete_InsideRolledBackTransaction_LeaveDatabaseUnchanged()
        {
            using (var db = CreateContext())
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                Assert.Equal(ReggaeTrackCount, await db.Tracks.Where(t => t.GenreId == ReggaeGenreId)
                    .ExecuteUpdateAsync(p => p.SetProperty(t => t.UnitPrice, t => t.UnitPrice + .20m)));
                Assert.Equal(BelgiumInvoiceLines, await db.InvoiceLines
                    .Where(il => il.Invoice!.Customer!.Country == Country).ExecuteDeleteAsync());

                await tx.RollbackAsync();
            }

            using var verify = CreateContext();
            var prices = await verify.Tracks.Where(t => t.GenreId == ReggaeGenreId).Select(t => t.UnitPrice).ToListAsync();
            Assert.All(prices, p => Assert.Equal(ReggaeOriginalPrice, Math.Round(p, 2)));
            Assert.Equal(BelgiumInvoiceLines, await verify.InvoiceLines.CountAsync(il => il.Invoice!.Customer!.Country == Country));
        }
    }
}
