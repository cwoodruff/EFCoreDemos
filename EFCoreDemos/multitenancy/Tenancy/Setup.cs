using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using multitenancy.Data;
using multitenancy.Model;

namespace multitenancy.Tenancy;

/// <summary>Where the demo's data lives: one shared database plus one database per tenant.</summary>
public sealed record DemoDatabases(string SharedConnectionString, TenantCatalog Catalog);

public static class Setup
{
    public static readonly string[] Tenants = ["acme", "globex"];

    public static readonly (string Tenant, string Name)[] SeedProjects =
    [
        ("acme", "Rocket skates"),
        ("acme", "Giant magnet"),
        ("acme", "Tornado kit"),
        ("globex", "Doomsday device"),
        ("globex", "Hammock district")
    ];

    /// <summary>Everything an app would register at startup. Every context is resolved per request (scope).</summary>
    public static IServiceCollection AddMultitenancyDemo(this IServiceCollection services, DemoDatabases databases)
    {
        services.AddScoped<TenantProvider>();
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantProvider>());
        services.AddSingleton(databases.Catalog);

        // 1. Shared database, tenant column + filter.
        services.AddDbContext<SharedContext>(o => o.UseSqlite(databases.SharedConnectionString));

        // The bug, registered exactly the same way.
        services.AddDbContext<LeakyContext>(o => o.UseSqlite(databases.SharedConnectionString));

        // 2. Database per tenant.
        services.AddScoped<IDbContextFactory<TenantDbContext>, TenantDbContextFactory>();

        // 3. A model that differs per tenant needs the tenant in the model cache key.
        services.AddDbContext<TablePerTenantContext>(o => o
            .UseSqlite(databases.SharedConnectionString)
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>());

        // 4. Pooling: the pool is a singleton; the scoped factory in front of it sets the tenant per lease.
        services.AddPooledDbContextFactory<PooledTenantContext>(o => o.UseSqlite(databases.SharedConnectionString));
        services.AddScoped<TenantScopedContextFactory>();

        return services;
    }

    /// <summary>Creates and fills every database the demo uses (tables are created fresh each run).</summary>
    public static void Seed(DemoDatabases databases)
    {
        // Shared database: every tenant's rows in one Projects table.
        using (var db = new SharedContext(SharedOptions(databases.SharedConnectionString), new TenantProvider()))
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            db.Projects.AddRange(SeedProjects.Select(p => new Project { TenantId = p.Tenant, Name = p.Name }));
            db.SaveChanges();
        }

        // Table per tenant, in the same shared database: Projects_acme, Projects_globex.
        foreach (var tenant in Tenants)
        {
            using var db = new TablePerTenantContext(TablePerTenantOptions(databases.SharedConnectionString),
                new TenantProvider { TenantId = tenant });
            db.Database.ExecuteSqlRaw(db.Database.GenerateCreateScript());
            db.Projects.AddRange(SeedProjects.Where(p => p.Tenant == tenant)
                .Select(p => new Project { TenantId = p.Tenant, Name = p.Name }));
            db.SaveChanges();
        }

        // Database per tenant: each tenant's rows in its own database.
        foreach (var tenant in Tenants)
        {
            using var db = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlite(databases.Catalog.ConnectionStringFor(tenant)).Options);
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            db.Projects.AddRange(SeedProjects.Where(p => p.Tenant == tenant)
                .Select(p => new Project { TenantId = p.Tenant, Name = p.Name }));
            db.SaveChanges();
        }
    }

    public static DbContextOptions<SharedContext> SharedOptions(string connectionString) =>
        new DbContextOptionsBuilder<SharedContext>().UseSqlite(connectionString).Options;

    public static DbContextOptions<TablePerTenantContext> TablePerTenantOptions(string connectionString) =>
        new DbContextOptionsBuilder<TablePerTenantContext>()
            .UseSqlite(connectionString)
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
            .Options;
}

/// <summary>Runs a piece of work the way a web request would: its own DI scope, with the tenant set first.</summary>
public static class Requests
{
    public static async Task<T> InTenantAsync<T>(this IServiceProvider root, string? tenantId,
        Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantProvider>().TenantId = tenantId; // "the middleware"
        return await work(scope.ServiceProvider);
    }
}
