using Microsoft.EntityFrameworkCore;

namespace PaginationBenchmark.Data;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Total).HasColumnType("decimal(18,2)");
            b.Property(o => o.Status).HasMaxLength(32);

            // Composite index supporting (CreatedAt, Id) keyset pagination.
            b.HasIndex(o => new { o.CreatedAt, o.Id })
                .HasDatabaseName("IX_Orders_CreatedAt_Id");
        });
    }
}
