using Microsoft.EntityFrameworkCore;
using testing_strategies.Infrastructure;
using testing_strategies.Store;

namespace testing_strategies;

/// <summary>
/// The test suite, written once. Every scenario asserts what SQL Server - the production database - actually
/// does. The three classes below run it unchanged against a different test double each; a failure means that
/// double would let the bug through.
/// </summary>
public abstract class Scenarios
{
    protected abstract Task<TestStore> CreateStoreAsync();

    // A failed import must leave nothing behind.
    protected async Task Transaction_Rollback_DiscardsWork()
    {
        await using var store = await CreateStoreAsync();

        await using (var db = store.CreateContext())
        {
            var import = new[] { ("Ada", "ada@example.com"), ("Grace", "grace@example.com"), ("Linus", "linus@example.com") };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new StoreService(db).ImportAsync(import, failAfter: n => n == 2));
        }

        await using var check = store.CreateContext();
        Assert.Equal(0, await check.Customers.CountAsync());
    }

    // Two customers can't share an e-mail address (unique index).
    protected async Task UniqueConstraint_IsEnforced()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();
        var service = new StoreService(db);

        await service.RegisterAsync("Ada", "ada@example.com");
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(() => service.RegisterAsync("Ada Again", "ada@example.com"));
    }

    // An order for a customer that doesn't exist (foreign key).
    protected async Task ForeignKey_IsEnforced()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(() => new StoreService(db).PlaceOrderAsync(customerId: 999, total: 10m));
    }

    // Name is nvarchar(50): SQL Server refuses to truncate.
    protected async Task MaxLength_IsEnforced()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new StoreService(db).RegisterAsync(new string('x', 60), "long@example.com"));
    }

    // SQL Server's default collation is case-insensitive: a user typing "ada lovelace" finds "Ada Lovelace".
    protected async Task CaseInsensitiveLookup_MatchesSqlServerCollation()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();
        var service = new StoreService(db);
        await service.RegisterAsync("Ada Lovelace", "ada@example.com");

        var found = await service.FindByNameAsync("ada lovelace");

        Assert.Single(found);
    }

    // Raw SQL written in plain ANSI SQL.
    protected async Task RawSql_Portable_Works()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();
        var service = new StoreService(db);
        await service.RegisterAsync("Ada", "ada@example.com");
        await service.RegisterAsync("Grace", "grace@navy.mil");

        var found = await service.SearchByEmailDomainSqlAsync("example.com");

        Assert.Equal(["Ada"], found.Select(c => c.Name));
    }

    // Raw SQL in the production dialect (TOP is T-SQL).
    protected async Task RawSql_SqlServerDialect_Works()
    {
        await using var store = await CreateStoreAsync();
        await using var db = store.CreateContext();
        var service = new StoreService(db);
        await service.RegisterAsync("Ada", "ada@example.com");
        await service.RegisterAsync("Grace", "grace@example.com");
        await service.RegisterAsync("Linus", "linus@example.com");

        var found = await service.FirstCustomersSqlAsync(2);

        Assert.Equal(["Ada", "Grace"], found.Select(c => c.Name));
    }

    // A set-based UPDATE that never loads the orders.
    protected async Task ExecuteUpdate_AppliesInDatabase()
    {
        await using var store = await CreateStoreAsync();
        int customerId;
        await using (var db = store.CreateContext())
        {
            var service = new StoreService(db);
            customerId = (await service.RegisterAsync("Ada", "ada@example.com")).Id;
            await service.PlaceOrderAsync(customerId, 100m);
            await service.PlaceOrderAsync(customerId, 50m);

            Assert.Equal(2, await service.ApplyDiscountAsync(customerId, percent: 10));
        }

        await using var check = store.CreateContext();
        var totals = await check.Orders.Select(o => o.Total).ToListAsync();
        Assert.Equal([45m, 90m], totals.Order());
    }
}
