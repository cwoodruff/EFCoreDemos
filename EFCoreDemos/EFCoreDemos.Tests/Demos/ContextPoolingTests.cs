extern alias context_pooling;

using context_pooling::context_pooling;
using context_pooling::context_pooling.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// context-pooling: AddDbContext gives a new context per scope, AddDbContextFactory a new context per call,
    /// and AddPooledDbContextFactory / AddDbContextPool hand the same instance back out (new lease, state reset)
    /// after it is disposed. Also checks the demo's transaction-across-SaveChanges rollback.
    /// </summary>
    public class ContextPoolingTests : IDisposable
    {
        private const int GenreCount = 25;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ServiceProvider BuildDemoServices()
        {
            var services = new ServiceCollection();
            services.AddDbContext<ScopedContext>(o => o.UseSqlite(_db.ConnectionString));
            services.AddDbContextFactory<FactoryContext>(o => o.UseSqlite(_db.ConnectionString));
            services.AddPooledDbContextFactory<PooledContext>(o => o.UseSqlite(_db.ConnectionString));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Scoped_SameInstanceWithinScope_NewInstancePerScope()
        {
            await using var sp = BuildDemoServices();

            ScopedContext first;
            await using (var scope = sp.CreateAsyncScope())
            {
                first = scope.ServiceProvider.GetRequiredService<ScopedContext>();
                Assert.Same(first, scope.ServiceProvider.GetRequiredService<ScopedContext>());
                Assert.Equal(GenreCount, await first.Genres.CountAsync());
            }

            await using (var scope = sp.CreateAsyncScope())
            {
                Assert.NotSame(first, scope.ServiceProvider.GetRequiredService<ScopedContext>());
            }
        }

        [Fact]
        public async Task Factory_CreatesANewInstanceOnEveryCall()
        {
            await using var sp = BuildDemoServices();
            var factory = sp.GetRequiredService<IDbContextFactory<FactoryContext>>();

            FactoryContext first;
            await using (first = await factory.CreateDbContextAsync())
            {
                Assert.Equal(GenreCount, await first.Genres.CountAsync());
            }

            await using var second = await factory.CreateDbContextAsync();
            Assert.NotSame(first, second);
            Assert.NotEqual(first.ContextId.InstanceId, second.ContextId.InstanceId);
        }

        [Fact]
        public async Task PooledFactory_ReusesInstance_AndIncrementsLease()
        {
            await using var sp = BuildDemoServices();
            var pooled = sp.GetRequiredService<IDbContextFactory<PooledContext>>();

            PooledContext first;
            DbContextId firstId;
            await using (first = await pooled.CreateDbContextAsync())
            {
                firstId = first.ContextId;
                Assert.Equal(GenreCount, await first.Genres.CountAsync());
            }

            await using var second = await pooled.CreateDbContextAsync();
            var secondId = second.ContextId;

            Assert.Same(first, second);
            Assert.Equal(firstId.InstanceId, secondId.InstanceId);
            Assert.Equal(firstId.Lease + 1, secondId.Lease);
        }

        [Fact]
        public async Task PooledFactory_ResetsStateBetweenLeases()
        {
            await using var sp = BuildDemoServices();
            var pooled = sp.GetRequiredService<IDbContextFactory<PooledContext>>();

            PooledContext first;
            await using (first = await pooled.CreateDbContextAsync())
            {
                var genre = await first.Genres.FirstAsync();
                genre.Name = "Changed but never saved";
                first.Playlists.Add(new Playlist { Name = "Never saved" });
                first.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                first.ChangeTracker.AutoDetectChangesEnabled = false;
                Assert.NotEmpty(first.ChangeTracker.Entries());
            }

            await using var second = await pooled.CreateDbContextAsync();

            Assert.Same(first, second);
            Assert.Empty(second.ChangeTracker.Entries());
            Assert.Equal(QueryTrackingBehavior.TrackAll, second.ChangeTracker.QueryTrackingBehavior);
            Assert.True(second.ChangeTracker.AutoDetectChangesEnabled);
            Assert.NotEqual("Changed but never saved", (await second.Genres.FirstAsync()).Name);
        }

        [Fact]
        public async Task AddDbContextPool_ReusesInstanceAcrossSequentialScopes_WhileAddDbContextDoesNot()
        {
            var pooledServices = new ServiceCollection();
            pooledServices.AddDbContextPool<ChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            await using var pooledSp = pooledServices.BuildServiceProvider();

            var plainServices = new ServiceCollection();
            plainServices.AddDbContext<ChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            await using var plainSp = plainServices.BuildServiceProvider();

            Assert.Single(await ResolveInSequentialScopes(pooledSp, 5));
            Assert.Equal(5, (await ResolveInSequentialScopes(plainSp, 5)).Count);
        }

        [Fact]
        public async Task TransactionAcrossSaveChanges_RollbackUndoesBothInserts()
        {
            using var db = new ChinookContext(_db.Options<ChinookContext>());
            var before = await db.Playlists.CountAsync();

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                db.Playlists.Add(new Playlist { Name = "Demo Playlist A" });
                await db.SaveChangesAsync();
                db.Playlists.Add(new Playlist { Name = "Demo Playlist B" });
                await db.SaveChangesAsync();

                Assert.Equal(before + 2, await db.Playlists.CountAsync());
                await tx.RollbackAsync();
            }

            using var fresh = new ChinookContext(_db.Options<ChinookContext>());
            Assert.Equal(before, await fresh.Playlists.CountAsync());
            Assert.False(await fresh.Playlists.AnyAsync(p => p.Name == "Demo Playlist A" || p.Name == "Demo Playlist B"));
        }

        // Mirrors the demo's request loop: one scope per "request", a GenreController doing one query.
        private static async Task<HashSet<ChinookContext>> ResolveInSequentialScopes(IServiceProvider sp, int requests)
        {
            var instances = new HashSet<ChinookContext>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < requests; i++)
            {
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ChinookContext>();
                await new GenreController(context).ActionAsync();
                instances.Add(context);
            }

            return instances;
        }
    }
}
