using Microsoft.EntityFrameworkCore;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy.Data;

/// <summary>
/// Shared database, tenant column. The tenant id is copied into a field when the context is created
/// (once per request), and the query filter reads that field.
/// </summary>
public class SharedContext(DbContextOptions<SharedContext> options, ITenantProvider tenant) : DbContext(options)
{
    // EF sees "this._tenantId" in the filter and turns it into a query parameter that it reads from the
    // current context instance on every query. The cached model holds the expression, not the value.
    private readonly string? _tenantId = tenant.TenantId;

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasIndex(p => p.TenantId);
            entity.HasQueryFilter(p => p.TenantId == _tenantId);
        });
    }

    // New rows are stamped with the current tenant, so application code never sets TenantId by hand.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<Project>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                entry.Entity.TenantId = _tenantId ?? throw new InvalidOperationException("No tenant: refusing to save a Project.");
            }
        }
    }
}
