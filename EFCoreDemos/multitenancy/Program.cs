using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using multitenancy.Data;
using multitenancy.Model;
using multitenancy.Tenancy;

namespace multitenancy;

internal static class Program
{
    public static async Task Main()
    {
        // Throwaway SQLite files: one shared database, one database per tenant. Deleted at the end.
        var folder = Path.Combine(Path.GetTempPath(), $"efdemo-multitenancy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string Connection(string name) => $"Data Source={Path.Combine(folder, name + ".db")};Pooling=False";

        var databases = new DemoDatabases(Connection("shared"),
            new TenantCatalog(Setup.Tenants.ToDictionary(t => t, Connection)));

        try
        {
            Setup.Seed(databases);

            var services = new ServiceCollection().AddMultitenancyDemo(databases);
            await using var root = services.BuildServiceProvider();

            await SharedDatabase(root);
            await DatabasePerTenant(root, databases);
            await ModelCachingGotcha(root);
            await ModelCacheKeyFactory(root, databases);
            await Pooling(root);
            ServiceProviderPerTenantTrap(databases);
            Guidance();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    // ------------------------------------------------------------------------------------------------
    private static async Task SharedDatabase(IServiceProvider root)
    {
        Header("1. Shared database, tenant column: a scoped ITenantProvider feeds a global query filter");
        Console.WriteLine("""
            public class SharedContext(DbContextOptions<SharedContext> options, ITenantProvider tenant) : DbContext(options)
            {
                private readonly string? _tenantId = tenant.TenantId;   // read once per request
                ...
                entity.HasQueryFilter(p => p.TenantId == _tenantId);    // a field of THIS context
            }
            """);

        await root.InTenantAsync("acme", async sp =>
        {
            var db = sp.GetRequiredService<SharedContext>();
            Console.WriteLine("-- request for acme --");
            Console.WriteLine(db.Projects.OrderBy(p => p.Id).ToQueryString());
            Print(await db.Projects.OrderBy(p => p.Id).ToListAsync());

            db.Projects.Add(new Project { Name = "Earthquake pills" }); // no TenantId set by the caller
            await db.SaveChangesAsync();
            var added = await db.Projects.SingleAsync(p => p.Name == "Earthquake pills");
            Console.WriteLine($"Added 'Earthquake pills' without setting TenantId; SaveChanges stamped it: TenantId = '{added.TenantId}'");
            return 0;
        });

        await root.InTenantAsync("globex", async sp =>
        {
            Console.WriteLine();
            Console.WriteLine("-- request for globex: same code, same cached model, different parameter value --");
            Print(await sp.GetRequiredService<SharedContext>().Projects.OrderBy(p => p.Id).ToListAsync());
            return 0;
        });

        await root.InTenantAsync(null, async sp =>
        {
            Console.WriteLine();
            var db = sp.GetRequiredService<SharedContext>();
            Console.WriteLine($"-- no tenant resolved: {await db.Projects.CountAsync()} rows (TenantId = NULL matches nothing - fails closed) --");
            Console.WriteLine($"-- admin report, IgnoreQueryFilters(): {await db.Projects.IgnoreQueryFilters().CountAsync()} rows across all tenants --");
            return 0;
        });
    }

    // ------------------------------------------------------------------------------------------------
    private static async Task DatabasePerTenant(IServiceProvider root, DemoDatabases databases)
    {
        Header("2. Database per tenant: an IDbContextFactory picks the connection string");
        Console.WriteLine("""
            public sealed class TenantDbContextFactory(ITenantProvider tenant, TenantCatalog catalog) : IDbContextFactory<TenantDbContext>
            {
                public TenantDbContext CreateDbContext() =>
                    new(new DbContextOptionsBuilder<TenantDbContext>()
                        .UseSqlite(catalog.ConnectionStringFor(tenant.TenantId)).Options);
            }
            """);

        foreach (var tenant in Setup.Tenants)
        {
            await root.InTenantAsync(tenant, async sp =>
            {
                await using var db = await sp.GetRequiredService<IDbContextFactory<TenantDbContext>>().CreateDbContextAsync();
                Console.WriteLine($"-- request for {tenant}: {Path.GetFileName(db.Database.GetDbConnection().DataSource)} --");
                Print(await db.Projects.OrderBy(p => p.Id).ToListAsync());
                return 0;
            });
        }

        await root.InTenantAsync("initech", sp =>
        {
            try
            {
                sp.GetRequiredService<IDbContextFactory<TenantDbContext>>().CreateDbContext();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"-- request for initech: {ex.Message} --");
            }

            return Task.FromResult(0);
        });

        Console.WriteLine();
        Console.WriteLine("No filter at all: isolation is physical. Only the connection string changes between tenants,");
        Console.WriteLine("so all of them share one internal service provider and one cached model.");
    }

    // ------------------------------------------------------------------------------------------------
    private static async Task ModelCachingGotcha(IServiceProvider root)
    {
        Header("3. The gotcha: EF builds the model ONCE - a filter that captures the tenant id leaks");
        Console.WriteLine("""
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var tenantId = tenant.TenantId;                         // a local, read while building the model
                entity.HasQueryFilter(p => p.TenantId == tenantId);    // captured for good
            }
            """);

        await root.InTenantAsync("acme", async sp =>
        {
            var names = await sp.GetRequiredService<LeakyContext>().Projects.Select(p => p.Name).ToListAsync();
            Console.WriteLine($"-- request for acme (builds the model):  {string.Join(", ", names)}");
            return 0;
        });

        await root.InTenantAsync("globex", async sp =>
        {
            var db = sp.GetRequiredService<LeakyContext>();
            var rows = await db.Projects.ToListAsync();
            Console.WriteLine($"-- request for globex (reuses it):       {string.Join(", ", rows.Select(p => p.Name))}");
            Console.WriteLine($"   ...rows owned by: {string.Join(", ", rows.Select(p => p.TenantId).Distinct())}   <-- globex is reading acme's data");
            Console.WriteLine();
            Console.WriteLine(db.Projects.ToQueryString());
            return 0;
        });

        Console.WriteLine();
        Console.WriteLine("OnModelCreating runs once per context type, and the model is cached for the life of the process.");
        Console.WriteLine("The captured value belongs to whichever request happened to build the model first.");
        Console.WriteLine("Fix (section 1): point the filter at a field or property of the context - EF parameterizes");
        Console.WriteLine("anything reached through 'this' and reads it from the current instance on every query.");
    }

    // ------------------------------------------------------------------------------------------------
    private static async Task ModelCacheKeyFactory(IServiceProvider root, DemoDatabases databases)
    {
        Header("4. When the model itself differs per tenant: IModelCacheKeyFactory");
        Console.WriteLine("""
            protected override void OnModelCreating(ModelBuilder modelBuilder) =>
                modelBuilder.Entity<Project>().ToTable($"Projects_{TenantId}");   // a different TABLE per tenant
            """);
        Console.WriteLine("A filter can't express a different table or schema - the model has to differ.");
        Console.WriteLine();

        Console.WriteLine("-- Without a cache key factory (default key = the context type) --");
        var plainOptions = new DbContextOptionsBuilder<TablePerTenantContext>().UseSqlite(databases.SharedConnectionString).Options;
        foreach (var tenant in Setup.Tenants)
        {
            await using var db = new TablePerTenantContext(plainOptions, new TenantProvider { TenantId = tenant });
            var sql = db.Projects.ToQueryString().Split('\n').Single(l => l.StartsWith("FROM"));
            var names = await db.Projects.Select(p => p.Name).ToListAsync();
            Console.WriteLine($"  {tenant,-7} {sql,-36} -> {string.Join(", ", names)}");
        }

        Console.WriteLine("  globex got acme's table: the first tenant's model was cached and reused.");
        Console.WriteLine();

        Console.WriteLine("""
            -- With .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>() --
              public object Create(DbContext context, bool designTime) =>
                  context is TablePerTenantContext t ? (context.GetType(), t.TenantId, designTime)
                                                     : (context.GetType(), designTime);
            """);
        foreach (var tenant in Setup.Tenants)
        {
            await root.InTenantAsync(tenant, async sp =>
            {
                var db = sp.GetRequiredService<TablePerTenantContext>();
                var sql = db.Projects.ToQueryString().Split('\n').Single(l => l.StartsWith("FROM"));
                var names = await db.Projects.Select(p => p.Name).ToListAsync();
                Console.WriteLine($"  {tenant,-7} {sql,-36} -> {string.Join(", ", names)}");
                return 0;
            });
        }

        Console.WriteLine();
        Console.WriteLine("One model per tenant, each built once and cached. Only needed when the SHAPE differs");
        Console.WriteLine("(table or schema names, entities, columns). On SQL Server, schema per tenant is the classic");
        Console.WriteLine("case: modelBuilder.HasDefaultSchema(TenantId). Tenant VALUES belong in a filter (section 1),");
        Console.WriteLine("not in the cache key - a model per tenant costs memory and a model build per tenant.");
    }

    // ------------------------------------------------------------------------------------------------
    private static async Task Pooling(IServiceProvider root)
    {
        Header("5. Context pooling: the context outlives the request, so its tenant must be reset");
        Console.WriteLine("A pooled context is built once and reused, so the tenant can't come in through the constructor;");
        Console.WriteLine("it's a settable TenantId property used by the filter. EF resets its own state on return to the");
        Console.WriteLine("pool (change tracker, options) - not yours.");
        Console.WriteLine();

        await root.InTenantAsync("acme", async sp =>
        {
            await using var db = await sp.GetRequiredService<IDbContextFactory<PooledTenantContext>>().CreateDbContextAsync();
            db.TenantId = "acme";
            Console.WriteLine($"-- request for acme   {db.ContextId}: TenantId set to 'acme', {await db.Projects.CountAsync()} rows --");
            return 0;
        });

        await root.InTenantAsync("globex", async sp =>
        {
            await using var db = await sp.GetRequiredService<IDbContextFactory<PooledTenantContext>>().CreateDbContextAsync();
            // This code path forgot to set db.TenantId.
            var rows = await db.Projects.ToListAsync();
            Console.WriteLine($"-- request for globex {db.ContextId}: TenantId never set, but it is '{db.TenantId}' --");
            Console.WriteLine($"   same instance, next lease -> {string.Join(", ", rows.Select(p => p.Name))}   <-- acme's rows");
            return 0;
        });

        Console.WriteLine();
        Console.WriteLine("""
            -- Fix: a scoped factory in front of the pool sets the tenant on every lease --
              services.AddPooledDbContextFactory<PooledTenantContext>(...);
              services.AddScoped<TenantScopedContextFactory>();   // pool.CreateDbContext() + context.TenantId = tenant.TenantId
            """);

        foreach (var tenant in Setup.Tenants)
        {
            await root.InTenantAsync(tenant, async sp =>
            {
                await using var db = sp.GetRequiredService<TenantScopedContextFactory>().CreateDbContext();
                var rows = await db.Projects.OrderBy(p => p.Id).ToListAsync();
                Console.WriteLine($"  {tenant,-7} {db.ContextId}  -> {string.Join(", ", rows.Select(p => p.Name))}");
                return 0;
            });
        }

        Console.WriteLine("Same pooled instance every time (InstanceId), right tenant every time.");
    }

    // ------------------------------------------------------------------------------------------------
    private static void ServiceProviderPerTenantTrap(DemoDatabases databases)
    {
        Header("6. Don't hand EF a new singleton per tenant");
        Console.WriteLine("Per-tenant connection strings are free: every tenant above shared one internal service provider.");
        Console.WriteLine("Some options are part of that provider, though - singleton interceptors (IMaterializationInterceptor,");
        Console.WriteLine("IQueryExpressionInterceptor) and ReplaceService types. A new instance per tenant means a whole new");
        Console.WriteLine("internal service provider, with its own empty model and query caches, per tenant:");
        Console.WriteLine();
        Console.WriteLine("""
                new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlite(catalog.ConnectionStringFor(tenantId))
                    .AddInterceptors(new TenantStampInterceptor(tenantId))   // a new singleton interceptor per tenant
            """);

        var connectionString = databases.Catalog.ConnectionStringFor("acme");
        for (var tenant = 1; tenant <= 30; tenant++)
        {
            try
            {
                var options = new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlite(connectionString)
                    .AddInterceptors(new TenantStampInterceptor($"tenant-{tenant}"))
                    .Options;
                using var db = new TenantDbContext(options);
                db.Projects.Count();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ManyServiceProvidersCreatedWarning"))
            {
                Console.WriteLine($"  tenants 1-{tenant - 1} worked; tenant #{tenant} threw InvalidOperationException:");
                Console.WriteLine($"  {ex.Message[..ex.Message.IndexOf(". This", StringComparison.Ordinal)]}.");
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("EF stops at 20 internal service providers per process (this demo had built a few already). Keep the");
        Console.WriteLine("tenant in data the context reads per query - a field, a filter, a connection string - and register");
        Console.WriteLine("interceptors once. (A new logger factory or DbCommandInterceptor per tenant does NOT trip this on");
        Console.WriteLine("EF Core 10: those are resolved per context, not baked into the provider.)");
    }

    // ------------------------------------------------------------------------------------------------
    private static void Guidance()
    {
        Header("Which pattern?");
        Console.WriteLine("""
              Shared database + tenant column   Cheapest, most tenants. Isolation depends on the filter being on every
                                                entity - one IgnoreQueryFilters() or raw SQL query can cross tenants.
              Database per tenant               Strongest isolation, per-tenant backup/restore and scaling. More databases
                                                to migrate and pay for. Same model, so no model-cache tricks.
              Schema/table per tenant           In between. Needs IModelCacheKeyFactory - one model per tenant in memory.

              Always: read the tenant from a scoped service, keep it in a context field (never a captured local), and
              set it on every lease when pooling.
            """);
    }

    private static void Print(IEnumerable<Project> projects)
    {
        foreach (var p in projects)
        {
            Console.WriteLine($"  #{p.Id,-2} {p.TenantId,-7} {p.Name}");
        }
    }

    private static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 100));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 100));
    }
}

/// <summary>A per-tenant singleton interceptor: stamps the tenant onto every entity it materializes.</summary>
internal sealed class TenantStampInterceptor(string tenantId) : IMaterializationInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Project project) project.TenantId = tenantId;
        return entity;
    }
}
