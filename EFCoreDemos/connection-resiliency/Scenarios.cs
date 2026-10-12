using connection_resiliency.Data;
using Microsoft.EntityFrameworkCore;

namespace connection_resiliency;

/// <summary>What a scenario did: the exception it surfaced (null = success) and which call threw it.</summary>
public sealed record Outcome(Exception? Error = null, string? ThrownBy = null)
{
    public bool Succeeded => Error is null;
}

/// <summary>The units of work the demo runs, shared with the tests.</summary>
public static class Scenarios
{
    public static Order NewOrder(string customer, decimal total) =>
        new() { Reference = Guid.NewGuid(), Customer = customer, Total = total };

    public static Payment PaymentFor(Order order) =>
        new() { OrderReference = order.Reference, Amount = order.Total };

    /// <summary>One SaveChanges, one INSERT. With EnableRetryOnFailure EF retries it on its own.</summary>
    public static async Task<Outcome> SaveOrderAsync(DbContextOptions<ResiliencyContext> options, Order order)
    {
        try
        {
            await using var db = new ResiliencyContext(options);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return new Outcome();
        }
        catch (Exception e)
        {
            return new Outcome(e, "SaveChangesAsync");
        }
    }

    /// <summary>A query: retried exactly like SaveChanges.</summary>
    public static async Task<(Outcome Outcome, int Count)> CountOrdersAsync(DbContextOptions<ResiliencyContext> options)
    {
        try
        {
            await using var db = new ResiliencyContext(options);
            return (new Outcome(), await db.Orders.CountAsync());
        }
        catch (Exception e)
        {
            return (new Outcome(e, "CountAsync"), -1);
        }
    }

    /// <summary>
    /// THE BUG: order + payment in a transaction the caller starts, with the retrying strategy switched on.
    /// </summary>
    public static async Task<Outcome> PlaceOrderInUserTransactionAsync(
        DbContextOptions<ResiliencyContext> options, Order order, Payment payment)
    {
        await using var db = new ResiliencyContext(options);

        var step = "BeginTransactionAsync";
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            step = "SaveChangesAsync (order)";
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            step = "SaveChangesAsync (payment)";
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            step = "CommitAsync";
            await transaction.CommitAsync();
            return new Outcome();
        }
        catch (Exception e)
        {
            return new Outcome(e, step);
        }
    }

    /// <summary>
    /// THE FIX: the whole unit of work - begin, both saves, commit - runs inside the execution strategy, so a
    /// transient failure anywhere replays all of it. A fresh context per attempt means nothing from a failed
    /// attempt is still tracked when the next one starts.
    /// </summary>
    /// <returns>How many times the strategy ran the unit of work.</returns>
    public static async Task<(Outcome Outcome, int Attempts)> PlaceOrderWithExecutionStrategyAsync(
        DbContextOptions<ResiliencyContext> options, Order order, Payment payment)
    {
        var attempts = 0;
        try
        {
            await using var db = new ResiliencyContext(options);
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                attempts++;
                await using var context = new ResiliencyContext(options);
                await using var transaction = await context.Database.BeginTransactionAsync();

                context.Orders.Add(Copy(order));
                await context.SaveChangesAsync();

                context.Payments.Add(Copy(payment));
                await context.SaveChangesAsync();

                await transaction.CommitAsync();
            });

            return (new Outcome(), attempts);
        }
        catch (Exception e)
        {
            return (new Outcome(e, "ExecuteAsync"), attempts);
        }
    }

    /// <summary>
    /// THE IDEMPOTENT VERSION: ExecuteInTransactionAsync begins and commits the transaction itself, and if the
    /// commit throws it calls verifySucceeded before retrying. Saving with acceptAllChangesOnSuccess: false keeps
    /// the entities Added until we know the commit really failed, so a retry can save them again.
    /// </summary>
    /// <returns>How many times the save ran, and how many times verifySucceeded was asked.</returns>
    public static async Task<(Outcome Outcome, int Saves, int Verifications)> PlaceOrderVerifiedAsync(
        DbContextOptions<ResiliencyContext> options, Order order, Payment payment)
    {
        var saves = 0;
        var verifications = 0;
        try
        {
            await using var db = new ResiliencyContext(options);
            db.Orders.Add(order);
            db.Payments.Add(payment);

            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteInTransactionAsync(
                db,
                operation: (context, cancellationToken) =>
                {
                    saves++;
                    return context.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                },
                verifySucceeded: (context, cancellationToken) =>
                {
                    verifications++;
                    return context.Orders.AsNoTracking()
                        .AnyAsync(o => o.Reference == order.Reference, cancellationToken);
                });

            db.ChangeTracker.AcceptAllChanges();
            return (new Outcome(), saves, verifications);
        }
        catch (Exception e)
        {
            return (new Outcome(e, "ExecuteInTransactionAsync"), saves, verifications);
        }
    }

    // Each attempt adds new instances: an entity from a failed attempt may already carry a generated key.
    private static Order Copy(Order o) => new() { Reference = o.Reference, Customer = o.Customer, Total = o.Total };
    private static Payment Copy(Payment p) => new() { OrderReference = p.OrderReference, Amount = p.Amount };
}
