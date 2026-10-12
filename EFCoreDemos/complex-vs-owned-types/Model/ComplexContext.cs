using Microsoft.EntityFrameworkCore;

namespace complex_vs_owned_types.Model;

public class ComplexCustomer
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Complex type, table-split into the Customers row.
    public required Address ShippingAddress { get; set; }

    // EF 10: optional complex type (null when every column is null).
    public Address? BillingAddress { get; set; }

    // EF 10: struct complex type.
    public GeoPoint Location { get; set; }

    // EF 10: complex collection mapped to a JSON column - no extra table, no keys.
    public List<Address> PreviousAddresses { get; set; } = [];
}

/// <summary>The same Address modeled as a complex type: a pure value with no identity.</summary>
public class ComplexContext(DbContextOptions<ComplexContext> options) : DbContext(options)
{
    public DbSet<ComplexCustomer> Customers => Set<ComplexCustomer>();

    public static DbContextOptions<ComplexContext> CreateOptions(string connectionString, Action<string>? log = null) =>
        Options.Create<ComplexContext>(connectionString, log);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplexCustomer>(entity =>
        {
            entity.ToTable("Customers");
            entity.ComplexProperty(c => c.ShippingAddress);
            entity.ComplexProperty(c => c.BillingAddress);
            entity.ComplexProperty(c => c.Location);
            entity.ComplexCollection(c => c.PreviousAddresses, a => a.ToJson());
        });
    }
}

internal static class Options
{
    public static DbContextOptions<T> Create<T>(string connectionString, Action<string>? log) where T : DbContext
    {
        var builder = new DbContextOptionsBuilder<T>().UseSqlite(connectionString);
        if (log != null)
        {
            builder.LogTo(log, [DbLoggerCategory.Database.Command.Name], Microsoft.Extensions.Logging.LogLevel.Information)
                .EnableSensitiveDataLogging();
        }

        return builder.Options;
    }
}
