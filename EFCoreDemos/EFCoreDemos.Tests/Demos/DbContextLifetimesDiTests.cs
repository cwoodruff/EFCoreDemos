extern alias dbcontext_lifetimes_di;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CaptiveSingletonService = dbcontext_lifetimes_di::CaptiveSingletonService;
using FactoryChinookContext = dbcontext_lifetimes_di::FactoryChinookContext;
using PooledChinookContext = dbcontext_lifetimes_di::PooledChinookContext;
using ScopedChinookContext = dbcontext_lifetimes_di::ScopedChinookContext;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// dbcontext-lifetimes-di: the same registrations the demo's Program.cs makes (AddDbContext /
    /// AddDbContextFactory / AddPooledDbContextFactory + a CaptiveSingletonService) resolved through a plain
    /// ServiceProvider, one scope per simulated request.
    /// </summary>
    public class DbContextLifetimesDiTests : IDisposable
    {
        private const int AlbumCount = 347;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ServiceProvider BuildDemoServices(bool validateScopes = false)
        {
            var services = new ServiceCollection();
            services.AddDbContext<ScopedChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            services.AddDbContextFactory<FactoryChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            services.AddPooledDbContextFactory<PooledChinookContext>(o => o.UseSqlite(_db.ConnectionString));
            services.AddSingleton<CaptiveSingletonService>();

            // The demo turns both off; ValidateScopes = true is what Development hosts use by default.
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = validateScopes,
                ValidateOnBuild = validateScopes
            });
        }

        [Fact]
        public async Task Scoped_SameInstanceWithinRequest_DifferentAcrossRequests()
        {
            await using var sp = BuildDemoServices();

            ScopedChinookContext first;
            await using (var request1 = sp.CreateAsyncScope())
            {
                first = request1.ServiceProvider.GetRequiredService<ScopedChinookContext>();
                Assert.Same(first, request1.ServiceProvider.GetRequiredService<ScopedChinookContext>());
                Assert.Equal(AlbumCount, await first.Albums.CountAsync());
            }

            await using var request2 = sp.CreateAsyncScope();
            Assert.NotSame(first, request2.ServiceProvider.GetRequiredService<ScopedChinookContext>());

            // The first request's context was disposed with its scope.
            await Assert.ThrowsAsync<ObjectDisposedException>(() => first.Albums.CountAsync());
        }

        [Fact]
        public async Task Factory_IsSingleton_AndCreatesANewContextPerCall()
        {
            await using var sp = BuildDemoServices();

            IDbContextFactory<FactoryChinookContext> fromScope1, fromScope2;
            await using (var s1 = sp.CreateAsyncScope()) fromScope1 = s1.ServiceProvider.GetRequiredService<IDbContextFactory<FactoryChinookContext>>();
            await using (var s2 = sp.CreateAsyncScope()) fromScope2 = s2.ServiceProvider.GetRequiredService<IDbContextFactory<FactoryChinookContext>>();
            Assert.Same(fromScope1, fromScope2);

            await using var a = await fromScope1.CreateDbContextAsync();
            await using var b = await fromScope1.CreateDbContextAsync();
            Assert.NotSame(a, b);
            Assert.Equal(AlbumCount, await a.Albums.CountAsync());
            Assert.Equal(AlbumCount, await b.Albums.CountAsync());
        }

        [Fact]
        public async Task PooledFactory_ReturnsSameInstanceAfterDispose_WithStateReset()
        {
            await using var sp = BuildDemoServices();
            var factory = sp.GetRequiredService<IDbContextFactory<PooledChinookContext>>();

            PooledChinookContext first;
            int firstLease;
            await using (first = await factory.CreateDbContextAsync())
            {
                firstLease = first.ContextId.Lease;
                var album = await first.Albums.FirstAsync();
                album.Title = "Dirty but never saved";
                Assert.Single(first.ChangeTracker.Entries());
            }

            await using var second = await factory.CreateDbContextAsync();
            Assert.Same(first, second);
            Assert.Equal(firstLease + 1, second.ContextId.Lease);
            Assert.Empty(second.ChangeTracker.Entries());
            Assert.Equal(AlbumCount, await second.Albums.CountAsync());
        }

        [Fact]
        public async Task CaptiveSingleton_FirstRequestWorks_SecondRequestHitsDisposedContext()
        {
            await using var sp = BuildDemoServices();
            var captive = sp.GetRequiredService<CaptiveSingletonService>();

            await using (var request1 = sp.CreateAsyncScope())
            {
                var (isFirstCall, count) = await captive.CountAlbumsAsync(
                    request1.ServiceProvider.GetRequiredService<ScopedChinookContext>());
                Assert.True(isFirstCall);
                Assert.Equal(AlbumCount, count);
            }

            await using var request2 = sp.CreateAsyncScope();
            var request2Context = request2.ServiceProvider.GetRequiredService<ScopedChinookContext>();

            // The singleton still holds request 1's (now disposed) context, not request 2's.
            await Assert.ThrowsAsync<ObjectDisposedException>(() => captive.CountAlbumsAsync(request2Context));
            Assert.Equal(AlbumCount, await request2Context.Albums.CountAsync());
        }

        [Fact]
        public async Task ValidateScopes_CatchesScopedContextResolvedFromRoot_ButNotTheMethodInjectedCaptive()
        {
            await using var sp = BuildDemoServices(validateScopes: true);

            // A scoped DbContext resolved from the root provider (what a singleton constructor dependency does).
            Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<ScopedChinookContext>());

            // CaptiveSingletonService has no constructor dependency on the context - it captures the one passed
            // to its method - so scope validation cannot see it and it still fails at runtime on the second request.
            var captive = sp.GetRequiredService<CaptiveSingletonService>();
            await using (var request1 = sp.CreateAsyncScope())
            {
                await captive.CountAlbumsAsync(request1.ServiceProvider.GetRequiredService<ScopedChinookContext>());
            }

            await using var request2 = sp.CreateAsyncScope();
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                captive.CountAlbumsAsync(request2.ServiceProvider.GetRequiredService<ScopedChinookContext>()));
        }
    }
}
