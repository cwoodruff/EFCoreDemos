using connection_resiliency.Faults;
using Microsoft.EntityFrameworkCore;

namespace connection_resiliency.Data;

public class Order
{
    public int Id { get; set; }

    /// <summary>Client-generated, so a retry can check whether this order already reached the database.</summary>
    public Guid Reference { get; set; }

    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public Guid OrderReference { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Demo-owned database (ResiliencyDemo by default) with Orders and Payments. Chinook is never touched.</summary>
public class ResiliencyContext(DbContextOptions<ResiliencyContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.Customer).HasMaxLength(100);
            entity.Property(e => e.Total).HasPrecision(10, 2);
            entity.HasIndex(e => e.Reference); // not unique on purpose: section 4 needs to be able to show a duplicate
        });

        modelBuilder.Entity<Payment>().Property(e => e.Amount).HasPrecision(10, 2);
    }

    /// <param name="retryOnFailure">Turns on SQL Server's retrying execution strategy (EnableRetryOnFailure).</param>
    /// <param name="errorNumbersToAdd">Extra SQL error numbers to treat as transient.</param>
    /// <param name="cacheServiceProvider">
    /// False gives every context its own internal service provider. Tests use that so they don't add to EF's
    /// process-wide provider cache (EF throws once a process has created more than 20).
    /// </param>
    public static DbContextOptions<ResiliencyContext> CreateOptions(
        string connectionString,
        FaultInjection? faults = null,
        bool retryOnFailure = true,
        int maxRetryCount = 3,
        int[]? errorNumbersToAdd = null,
        bool cacheServiceProvider = true)
    {
        var builder = new DbContextOptionsBuilder<ResiliencyContext>()
            .UseSqlServer(connectionString, sql =>
            {
                if (retryOnFailure)
                {
                    // Production code keeps the default delays (exponential back-off up to 30 s). The demo caps
                    // the delay so a run with a dozen retries still finishes instantly.
                    sql.EnableRetryOnFailure(maxRetryCount, TimeSpan.FromMilliseconds(10), errorNumbersToAdd ?? []);
                }
            })
            .EnableServiceProviderCaching(cacheServiceProvider);

        if (faults != null) builder.AddInterceptors(faults.Interceptors);
        return builder.Options;
    }

    /// <summary>Creates the database if missing and empties both tables, so every run starts the same.</summary>
    public static async Task ResetAsync(string connectionString)
    {
        await using var db = new ResiliencyContext(CreateOptions(connectionString, retryOnFailure: false));
        await db.Database.EnsureCreatedAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
    }

    public static async Task<(int Orders, int Payments)> CountAsync(string connectionString, Guid reference)
    {
        await using var db = new ResiliencyContext(CreateOptions(connectionString, retryOnFailure: false));
        return (await db.Orders.CountAsync(o => o.Reference == reference),
            await db.Payments.CountAsync(p => p.OrderReference == reference));
    }

    public static async Task DropAsync(string connectionString)
    {
        await using var db = new ResiliencyContext(CreateOptions(connectionString, retryOnFailure: false));
        await db.Database.EnsureDeletedAsync();
    }
}
