extern alias multitenancy;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using multitenancy::multitenancy.Data;
using multitenancy::multitenancy.Tenancy;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// multitenancy: shared database + tenant filter, database per tenant via a factory, the captured-tenant
    /// model-caching leak and its fix, IModelCacheKeyFactory for per-tenant models, and pooled-context resets.
    /// Each test gets fresh SQLite files and its own DI container (so its own context pool).
    ///
    /// The two BUGGY cases depend on which tenant builds the cached model first, so those contexts run on a
    /// private EF internal service provider (UseInternalServiceProvider) with an empty model cache. That also
    /// keeps them out of EF's process-wide provider cache, which other test classes share.
    /// </summary>
    public class MultitenancyTests : IDisposable
    {
        private readonly TempSqliteFile _shared = new();
        private readonly TempSqliteFile _acme = new();
        private readonly TempSqliteFile _globex = new();
        private readonly DemoDatabases _databases;
        private readonly ServiceProvider _root;

        public MultitenancyTests()
        {
            _databases = new DemoDatabases(_shared.ConnectionString, new TenantCatalog(new Dictionary<string, string>
            {
                ["acme"] = _acme.ConnectionString,
                ["globex"] = _globex.ConnectionString
            }));
            Setup.Seed(_databases);
            _root = new ServiceCollection().AddMultitenancyDemo(_databases).BuildServiceProvider();
        }

        public void Dispose()
        {
            _root.Dispose();
            _shared.Dispose();
            _acme.Dispose();
            _globex.Dispose();
        }

        private static string[] SeedNames(string tenant) =>
            Setup.SeedProjects.Where(p => p.Tenant == tenant).Select(p => p.Name).ToArray();

        /// <summary>A private EF service provider: its own, empty model cache.</summary>
        private static ServiceProvider IsolatedEfServices() =>
            new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        [Fact]
        public async Task SharedDatabase_FilterShowsEachTenantOnlyItsOwnRows()
        {
            foreach (var tenant in Setup.Tenants)
            {
                var rows = await _root.InTenantAsync(tenant, sp =>
                    sp.GetRequiredService<SharedContext>().Projects.OrderBy(p => p.Id).ToListAsync());

                Assert.Equal(SeedNames(tenant), rows.Select(p => p.Name));
                Assert.All(rows, p => Assert.Equal(tenant, p.TenantId));
            }
        }

        [Fact]
        public async Task SharedDatabase_NoTenantSeesNothing_IgnoreQueryFiltersSeesAll()
        {
            var (none, all) = await _root.InTenantAsync(null, async sp =>
            {
                var db = sp.GetRequiredService<SharedContext>();
                return (await db.Projects.CountAsync(), await db.Projects.IgnoreQueryFilters().CountAsync());
            });

            Assert.Equal(0, none);
            Assert.Equal(Setup.SeedProjects.Length, all);
        }

        [Fact]
        public async Task SharedDatabase_FilterIsAParameter_AndSaveChangesStampsTheTenant()
        {
            var sql = await _root.InTenantAsync("acme", async sp =>
            {
                var db = sp.GetRequiredService<SharedContext>();
                db.Projects.Add(new multitenancy::multitenancy.Model.Project { Name = "Earthquake pills" });
                await db.SaveChangesAsync();
                return db.Projects.ToQueryString();
            });

            Assert.Contains("@ef_filter___tenantId", sql);
            Assert.DoesNotContain("'acme'", sql.Split('\n').Last(l => l.StartsWith("WHERE")));

            var stamped = await _root.InTenantAsync(null, sp =>
                sp.GetRequiredService<SharedContext>().Projects.IgnoreQueryFilters().SingleAsync(p => p.Name == "Earthquake pills"));
            Assert.Equal("acme", stamped.TenantId);
        }

        [Fact]
        public async Task DatabasePerTenant_FactoryRoutesEachTenantToItsOwnDatabase()
        {
            foreach (var tenant in Setup.Tenants)
            {
                var (dataSource, names) = await _root.InTenantAsync(tenant, async sp =>
                {
                    await using var db = await sp.GetRequiredService<IDbContextFactory<TenantDbContext>>().CreateDbContextAsync();
                    return (db.Database.GetDbConnection().DataSource,
                        await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
                });

                var expectedFile = tenant == "acme" ? _acme.Path : _globex.Path;
                Assert.Equal(expectedFile, dataSource);
                Assert.Equal(SeedNames(tenant), names);
            }

            var ex = await _root.InTenantAsync("initech", sp => Task.FromResult(Assert.Throws<InvalidOperationException>(
                () => sp.GetRequiredService<IDbContextFactory<TenantDbContext>>().CreateDbContext())));
            Assert.Contains("initech", ex.Message);
        }

        [Theory]
        [InlineData("acme", "globex")]
        [InlineData("globex", "acme")]
        public async Task CapturedTenantInFilter_LeaksTheFirstTenantsRowsToEveryone(string first, string second)
        {
            await using var ef = IsolatedEfServices();
            var options = new DbContextOptionsBuilder<LeakyContext>()
                .UseSqlite(_databases.SharedConnectionString)
                .UseInternalServiceProvider(ef)
                .Options;

            await using (var db = new LeakyContext(options, new TenantProvider { TenantId = first }))
            {
                Assert.Equal(SeedNames(first), await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
            }

            await using (var db = new LeakyContext(options, new TenantProvider { TenantId = second }))
            {
                var rows = await db.Projects.OrderBy(p => p.Id).ToListAsync();

                // The second tenant gets the first tenant's rows: the model (and its filter value) was cached.
                Assert.Equal(SeedNames(first), rows.Select(p => p.Name));
                Assert.All(rows, p => Assert.Equal(first, p.TenantId));
                Assert.Contains($"= '{first}'", db.Projects.ToQueryString());
            }
        }

        [Fact]
        public async Task FieldInFilter_SameOrderAsTheLeak_EachTenantGetsItsOwnRows()
        {
            await using var ef = IsolatedEfServices();
            var options = new DbContextOptionsBuilder<SharedContext>()
                .UseSqlite(_databases.SharedConnectionString)
                .UseInternalServiceProvider(ef)
                .Options;

            foreach (var tenant in Setup.Tenants)
            {
                await using var db = new SharedContext(options, new TenantProvider { TenantId = tenant });
                Assert.Equal(SeedNames(tenant), await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
            }
        }

        [Fact]
        public async Task PerTenantModel_WithoutCacheKeyFactory_ReusesTheFirstTenantsTable()
        {
            await using var ef = IsolatedEfServices();
            var options = new DbContextOptionsBuilder<TablePerTenantContext>()
                .UseSqlite(_databases.SharedConnectionString)
                .UseInternalServiceProvider(ef)
                .Options;

            await using (var acme = new TablePerTenantContext(options, new TenantProvider { TenantId = "acme" }))
            {
                Assert.Contains("FROM \"Projects_acme\"", acme.Projects.ToQueryString());
            }

            await using var globex = new TablePerTenantContext(options, new TenantProvider { TenantId = "globex" });
            Assert.Contains("FROM \"Projects_acme\"", globex.Projects.ToQueryString());
            Assert.Equal(SeedNames("acme"), await globex.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
        }

        [Fact]
        public async Task PerTenantModel_WithCacheKeyFactory_BuildsOneModelPerTenant()
        {
            foreach (var tenant in Setup.Tenants)
            {
                var (sql, names) = await _root.InTenantAsync(tenant, async sp =>
                {
                    var db = sp.GetRequiredService<TablePerTenantContext>();
                    return (db.Projects.ToQueryString(), await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
                });

                Assert.Contains($"FROM \"Projects_{tenant}\"", sql);
                Assert.Equal(SeedNames(tenant), names);
            }

            var factory = new TenantModelCacheKeyFactory();
            await using var a = new TablePerTenantContext(Setup.TablePerTenantOptions(_databases.SharedConnectionString), new TenantProvider { TenantId = "acme" });
            await using var g = new TablePerTenantContext(Setup.TablePerTenantOptions(_databases.SharedConnectionString), new TenantProvider { TenantId = "globex" });
            Assert.NotEqual(factory.Create(a, false), factory.Create(g, false));
            Assert.NotSame(a.Model, g.Model);
        }

        [Fact]
        public async Task PooledContext_KeepsThePreviousLeasesTenant_WhenNotReset()
        {
            var pool = _root.GetRequiredService<IDbContextFactory<PooledTenantContext>>();

            Guid firstInstance;
            await using (var db = await pool.CreateDbContextAsync())
            {
                db.TenantId = "acme";
                firstInstance = db.ContextId.InstanceId;
                Assert.Equal(SeedNames("acme").Length, await db.Projects.CountAsync());
            }

            await using (var db = await pool.CreateDbContextAsync())
            {
                // A request for another tenant that forgot to set TenantId: same instance, stale tenant.
                Assert.Equal(firstInstance, db.ContextId.InstanceId);
                Assert.Equal(2, db.ContextId.Lease);
                Assert.Equal("acme", db.TenantId);
                Assert.Equal(SeedNames("acme"), await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync());
            }
        }

        [Fact]
        public async Task PooledContext_ScopedFactorySetsTheTenantOnEveryLease()
        {
            var instances = new HashSet<Guid>();

            foreach (var tenant in new[] { "acme", "globex", "acme", "globex" })
            {
                var names = await _root.InTenantAsync(tenant, async sp =>
                {
                    await using var db = sp.GetRequiredService<TenantScopedContextFactory>().CreateDbContext();
                    instances.Add(db.ContextId.InstanceId);
                    Assert.Equal(tenant, db.TenantId);
                    return await db.Projects.OrderBy(p => p.Id).Select(p => p.Name).ToListAsync();
                });

                Assert.Equal(SeedNames(tenant), names);
            }

            Assert.Single(instances); // one pooled instance served every request
        }
    }
}
