using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy.Data;

/// <summary>
/// The MODEL itself differs per tenant: each tenant has its own table (Projects_acme, Projects_globex).
/// On SQL Server the usual form is schema per tenant - HasDefaultSchema(tenantId) - with the same problem.
/// A filter can't express that; the model has to be built once per tenant, which takes an IModelCacheKeyFactory.
/// </summary>
public class TablePerTenantContext(DbContextOptions<TablePerTenantContext> options, ITenantProvider tenant) : DbContext(options)
{
    public string TenantId { get; } = tenant.TenantId ?? throw new InvalidOperationException("No tenant.");

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Project>().ToTable($"Projects_{TenantId}");
}

/// <summary>
/// EF's default cache key is the context type, so one model serves every tenant. Adding the tenant id to
/// the key makes EF build (and cache) one model per tenant.
/// </summary>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is TablePerTenantContext tenantContext
            ? (context.GetType(), tenantContext.TenantId, designTime)
            : (context.GetType(), designTime);
}
