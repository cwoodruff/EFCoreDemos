using Microsoft.EntityFrameworkCore;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy.Data;

/// <summary>
/// For context pooling. A pooled context is constructed once and then reused across requests, so it can't
/// take the scoped ITenantProvider in its constructor - the tenant is a settable property instead. EF resets
/// its own state (change tracker, options) when a context goes back to the pool, but not your properties.
/// </summary>
public class PooledTenantContext(DbContextOptions<PooledTenantContext> options) : DbContext(options)
{
    public string? TenantId { get; set; }

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasQueryFilter(p => p.TenantId == TenantId);
        });
}

/// <summary>
/// The fix: a scoped factory in front of the pool sets the tenant on EVERY lease, so no code path can
/// use a context that still carries the previous request's tenant.
/// </summary>
public sealed class TenantScopedContextFactory(IDbContextFactory<PooledTenantContext> pool, ITenantProvider tenant)
    : IDbContextFactory<PooledTenantContext>
{
    public PooledTenantContext CreateDbContext()
    {
        var context = pool.CreateDbContext();
        context.TenantId = tenant.TenantId;
        return context;
    }
}
