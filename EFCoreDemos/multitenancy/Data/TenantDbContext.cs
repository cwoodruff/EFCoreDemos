using Microsoft.EntityFrameworkCore;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy.Data;

/// <summary>Database per tenant: no filter needed, each tenant's rows live in a different database.</summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Project>().ToTable("Projects");
}

/// <summary>
/// Scoped factory: picks the current tenant's connection string. Only the connection string differs
/// between tenants, so every context shares one internal service provider and one cached model.
/// </summary>
public sealed class TenantDbContextFactory(ITenantProvider tenant, TenantCatalog catalog) : IDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite(catalog.ConnectionStringFor(tenant.TenantId))
            .Options);
}
