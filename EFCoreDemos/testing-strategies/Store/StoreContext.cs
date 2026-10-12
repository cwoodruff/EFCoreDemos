using Microsoft.EntityFrameworkCore;

namespace testing_strategies.Store;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public List<Order> Orders { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// The production model. It is written for SQL Server, which is what the app runs on: every constraint
/// below is enforced there. The question this project asks is which test doubles notice when code breaks them.
/// </summary>
public class StoreContext(DbContextOptions<StoreContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(50);
            entity.Property(c => c.Email).HasMaxLength(200);
            entity.HasIndex(c => c.Email).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.Total).HasPrecision(10, 2);
            entity.HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId);
        });
    }
}
