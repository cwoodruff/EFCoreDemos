extern alias demo_4_entity_counters;

using demo_4_entity_counters::Demos;
using demo_4_entity_counters::Demos.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// demo-4-entity-counters: with AddDbContextPool a scope rents a context instance from the pool and returns it
    /// (reset) on dispose, so sequential requests reuse one instance and ChinookContext.InstanceCount barely moves.
    /// InstanceCount is static, so the counter assertions use deltas; tests in this class run sequentially.
    /// </summary>
    public class Demo4EntityCountersTests : IDisposable
    {
        private const int Requests = 10;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ServiceProvider BuildProvider(bool pooled)
        {
            var services = new ServiceCollection();
            if (pooled)
            {
                services.AddDbContextPool<ChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            }
            else
            {
                services.AddDbContext<ChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            }

            return services.BuildServiceProvider();
        }

        private static async Task<List<ChinookContext>> SimulateRequestsAsync(IServiceProvider provider, int count)
        {
            var contexts = new List<ChinookContext>();
            for (var i = 0; i < count; i++)
            {
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ChinookContext>();
                await new GenreController(context).ActionAsync();
                contexts.Add(context);
            }

            return contexts;
        }

        [Fact]
        public async Task Pooled_SequentialRequests_ReuseOneContextInstance()
        {
            await using var provider = BuildProvider(pooled: true);
            var before = Interlocked.Read(ref ChinookContext.InstanceCount);

            var contexts = await SimulateRequestsAsync(provider, Requests);

            Assert.Single(contexts.Distinct());
            Assert.Equal(1, Interlocked.Read(ref ChinookContext.InstanceCount) - before);
        }

        [Fact]
        public async Task NonPooled_EveryRequestCreatesANewContext()
        {
            await using var provider = BuildProvider(pooled: false);
            var before = Interlocked.Read(ref ChinookContext.InstanceCount);

            var contexts = await SimulateRequestsAsync(provider, Requests);

            Assert.Equal(Requests, contexts.Distinct().Count());
            Assert.Equal(Requests, Interlocked.Read(ref ChinookContext.InstanceCount) - before);
        }

        [Fact]
        public async Task Pooled_ContextIsResetBeforeReuse()
        {
            await using var provider = BuildProvider(pooled: true);

            ChinookContext first;
            using (var scope = provider.CreateScope())
            {
                first = scope.ServiceProvider.GetRequiredService<ChinookContext>();
                await new GenreController(first).ActionAsync();
                Assert.Single(first.ChangeTracker.Entries<Genre>());
            }

            using (var scope = provider.CreateScope())
            {
                var second = scope.ServiceProvider.GetRequiredService<ChinookContext>();
                Assert.Same(first, second);
                Assert.Empty(second.ChangeTracker.Entries());
            }
        }

        [Fact]
        public void Startup_RegistersAPooledContext()
        {
            var services = new ServiceCollection();
            new Startup().ConfigureServices(services);
            using var provider = services.BuildServiceProvider();

            // Resolve only (no queries), so the relative chinook.db in Startup is never opened.
            ChinookContext first;
            using (var scope = provider.CreateScope())
            {
                first = scope.ServiceProvider.GetRequiredService<ChinookContext>();
            }

            using (var scope = provider.CreateScope())
            {
                Assert.Same(first, scope.ServiceProvider.GetRequiredService<ChinookContext>());
            }
        }
    }
}
