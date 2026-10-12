using Microsoft.EntityFrameworkCore;

namespace complex_vs_owned_types.Model;

public class OwnedCustomer
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // OwnsOne, table-split into the Customers row.
    public required Address ShippingAddress { get; set; }

    // OwnsOne, optional: owned navigations have always been allowed to be null.
    public Address? BillingAddress { get; set; }

    // OwnsMany: its own table with a hidden key and a foreign key back to the customer.
    public List<Address> PreviousAddresses { get; set; } = [];
}

/// <summary>Address modeled as an owned entity type (the pre-EF 8 way to do value objects).</summary>
public class OwnedContext(DbContextOptions<OwnedContext> options) : DbContext(options)
{
    public DbSet<OwnedCustomer> Customers => Set<OwnedCustomer>();

    public static DbContextOptions<OwnedContext> CreateOptions(string connectionString, Action<string>? log = null) =>
        Options.Create<OwnedContext>(connectionString, log);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OwnedCustomer>(entity =>
        {
            entity.ToTable("Customers");
            entity.OwnsOne(c => c.ShippingAddress);
            entity.OwnsOne(c => c.BillingAddress);
            entity.OwnsMany(c => c.PreviousAddresses, a =>
            {
                a.ToTable("PreviousAddresses");
                // A single shadow "Id" key, so SQLite can generate it (the default composite
                // (OwnedCustomerId, Id) key can't be AUTOINCREMENT on SQLite).
                a.HasKey("Id");
            });
        });
    }
}
