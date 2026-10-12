using Microsoft.EntityFrameworkCore;

namespace testing_strategies.Store;

/// <summary>The small piece of application code under test. Every test calls through this, the way an app would.</summary>
public class StoreService(StoreContext db)
{
    public async Task<Customer> RegisterAsync(string name, string email)
    {
        var customer = new Customer { Name = name, Email = email };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public async Task<Order> PlaceOrderAsync(int customerId, decimal total)
    {
        var order = new Order { CustomerId = customerId, Total = total };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    /// <summary>Registers a batch atomically: if any customer fails, none are kept.</summary>
    public async Task ImportAsync(IEnumerable<(string Name, string Email)> customers, Func<int, bool> failAfter)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var count = 0;
        foreach (var (name, email) in customers)
        {
            db.Customers.Add(new Customer { Name = name, Email = email });
            await db.SaveChangesAsync();
            if (failAfter(++count))
            {
                throw new InvalidOperationException($"import aborted after {count} customer(s)");
            }
        }

        await transaction.CommitAsync();
    }

    /// <summary>A lookup by name, as typed by a user.</summary>
    public Task<List<Customer>> FindByNameAsync(string name) =>
        db.Customers.Where(c => c.Name == name).ToListAsync();

    /// <summary>Hand-written SQL in plain ANSI SQL.</summary>
    public Task<List<Customer>> SearchByEmailDomainSqlAsync(string domain) =>
        db.Customers.FromSql($"SELECT * FROM Customers WHERE Email LIKE {"%@" + domain}").ToListAsync();

    /// <summary>Hand-written SQL in the production dialect (T-SQL: TOP).</summary>
    public Task<List<Customer>> FirstCustomersSqlAsync(int count) =>
        db.Customers.FromSql($"SELECT TOP ({count}) * FROM Customers ORDER BY Id").ToListAsync();

    /// <summary>A set-based update: one UPDATE statement, no entities loaded.</summary>
    public Task<int> ApplyDiscountAsync(int customerId, decimal percent) =>
        db.Orders.Where(o => o.CustomerId == customerId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Total, o => o.Total * (1 - percent / 100)));
}
