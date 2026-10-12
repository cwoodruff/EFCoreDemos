using Microsoft.EntityFrameworkCore;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy.Data;

/// <summary>
/// THE BUG. Looks just like <see cref="SharedContext"/>, but the filter captures a local copy of the
/// tenant id inside OnModelCreating. OnModelCreating runs once per context type, the model is cached,
/// and every later request reuses the first request's tenant id.
/// </summary>
public class LeakyContext(DbContextOptions<LeakyContext> options, ITenantProvider tenant) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenantId = tenant.TenantId; // evaluated ONCE, for whichever request builds the model first

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasQueryFilter(p => p.TenantId == tenantId);
        });
    }
}
