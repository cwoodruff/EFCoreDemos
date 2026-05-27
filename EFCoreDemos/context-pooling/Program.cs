using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using context_pooling.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace context_pooling;

public class GenreController(ChinookContext? context)
{
    public async Task ActionAsync() => await context!.Genres.FirstAsync();
}

public class Program
{
    private static readonly string ConnectionString = $"Data Source={Path.Combine(AppContext.BaseDirectory, "chinook.db")}";
    private const int Threads = 32;
    private const int Seconds = 5;

    private static long _requestsProcessed;

    private static async Task Main()
    {
        await DemoDbContextLifetimes();
        Console.WriteLine();

        await DemoTransactionAcrossSaveChanges();
        Console.WriteLine();

        await DemoThroughputComparison();
    }

    // 1) Three ways to register a DbContext, side by side.
    private static async Task DemoDbContextLifetimes()
    {
        Console.WriteLine("== DbContext lifetimes (Scoped / Factory / Pooled) ==");

        var services = new ServiceCollection();
        services.AddDbContext<ScopedContext>(o => o.UseSqlite(ConnectionString));
        services.AddDbContextFactory<FactoryContext>(o => o.UseSqlite(ConnectionString));
        services.AddPooledDbContextFactory<PooledContext>(o => o.UseSqlite(ConnectionString));

        await using var sp = services.BuildServiceProvider();

        // Scoped: a fresh DbContext per scope (per HTTP request in ASP.NET).
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScopedContext>();
            Console.WriteLine($"  scoped   #{db.GetHashCode(),11} - genre count {await db.Genres.CountAsync()}");
        }

        // Factory: caller controls the lifetime (Blazor Server, background services).
        var factory = sp.GetRequiredService<IDbContextFactory<FactoryContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Console.WriteLine($"  factory  #{db.GetHashCode(),11} - genre count {await db.Genres.CountAsync()}");
        }

        // Pooled: returned to the pool on dispose, internal state reset.
        var pooled = sp.GetRequiredService<IDbContextFactory<PooledContext>>();
        await using (var db = await pooled.CreateDbContextAsync())
        {
            Console.WriteLine($"  pooled   #{db.GetHashCode(),11} - genre count {await db.Genres.CountAsync()}");
        }
    }

    // 2) Transaction spanning multiple SaveChanges, with rollback when something fails.
    private static async Task DemoTransactionAcrossSaveChanges()
    {
        Console.WriteLine("== Transaction across multiple SaveChanges (with rollback) ==");

        var options = new DbContextOptionsBuilder<ChinookContext>().UseSqlite(ConnectionString).Options;
        using var db = new ChinookContext(options);

        var playlistCountBefore = await db.Playlists.CountAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            db.Playlists.Add(new Playlist { Name = "Demo Playlist A" });
            await db.SaveChangesAsync();

            db.Playlists.Add(new Playlist { Name = "Demo Playlist B" });
            await db.SaveChangesAsync();

            // Simulate a failure after two successful SaveChanges calls.
            throw new InvalidOperationException("Simulated failure after two SaveChanges.");

#pragma warning disable CS0162 // Unreachable code - kept for clarity in the demo.
            await tx.CommitAsync();
#pragma warning restore CS0162
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Caught: {ex.Message}");
            await tx.RollbackAsync();
            Console.WriteLine("  Rolled back - both inserts undone in a single transaction.");
        }

        var playlistCountAfter = await db.Playlists.CountAsync();
        Console.WriteLine($"  Playlist delta after rollback: {playlistCountAfter - playlistCountBefore} (expected 0).");
    }

    // 3) Compare non-pooled and pooled throughput under the same load.
    private static async Task DemoThroughputComparison()
    {
        await DemoThroughputScenarioAsync(
            "DbContext throughput without pooling",
            services => services.AddDbContext<ChinookContext>(ConfigureContext));
        Console.WriteLine();

        await DemoThroughputScenarioAsync(
            "DbContext throughput with pooling",
            services => services.AddDbContextPool<ChinookContext>(ConfigureContext));
    }

    private static async Task DemoThroughputScenarioAsync(
        string heading,
        Action<IServiceCollection> registerContext)
    {
        Console.WriteLine($"== {heading} ({Threads} threads, {Seconds}s) ==");

        var serviceCollection = new ServiceCollection();
        registerContext(serviceCollection);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        ResetThroughputCounters();

        var stopwatch = new Stopwatch();
        var monitor = MonitorResults(TimeSpan.FromSeconds(Seconds), stopwatch);

        await Task.WhenAll(
            Enumerable
                .Range(0, Threads)
                .Select(_ => SimulateRequestsAsync(serviceProvider, stopwatch)));

        await monitor;
    }

    private static void ConfigureContext(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .UseSqlite(ConnectionString)
            .EnableSensitiveDataLogging();

    private static void ResetThroughputCounters()
    {
        Interlocked.Exchange(ref _requestsProcessed, 0);
        Interlocked.Exchange(ref ChinookContext.InstanceCount, 0);
    }

    private static async Task SimulateRequestsAsync(IServiceProvider serviceProvider, Stopwatch stopwatch)
    {
        while (stopwatch.IsRunning)
        {
            using (var serviceScope = serviceProvider.CreateScope())
            {
                await new GenreController(serviceScope.ServiceProvider.GetRequiredService<ChinookContext>()).ActionAsync();
            }

            Interlocked.Increment(ref _requestsProcessed);
        }
    }

    private static async Task MonitorResults(TimeSpan duration, Stopwatch stopwatch)
    {
        var lastInstanceCount = 0L;
        var lastRequestCount = 0L;
        var lastElapsed = TimeSpan.Zero;

        stopwatch.Start();

        while (stopwatch.Elapsed < duration)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            var instanceCount = ChinookContext.InstanceCount;
            var requestCount = _requestsProcessed;
            var elapsed = stopwatch.Elapsed;
            var currentElapsed = elapsed - lastElapsed;
            var currentRequests = requestCount - lastRequestCount;

            Console.WriteLine(
                $"  [{DateTime.Now:HH:mm:ss.fff}] "
                + $"Context creations/sec: {instanceCount - lastInstanceCount,6} | "
                + $"Requests/sec: {Math.Round(currentRequests / currentElapsed.TotalSeconds),8}");

            lastInstanceCount = instanceCount;
            lastRequestCount = requestCount;
            lastElapsed = elapsed;
        }

        Console.WriteLine();
        Console.WriteLine($"  Total context creations: {ChinookContext.InstanceCount}");
        Console.WriteLine($"  Requests per second:     {Math.Round(_requestsProcessed / stopwatch.Elapsed.TotalSeconds)}");

        stopwatch.Stop();
    }
}

// Marker subtypes so each DI registration owns its own DbContextOptions<T>.
public class ScopedContext : ChinookContext
{
    public ScopedContext(DbContextOptions<ScopedContext> options) : base(options) { }
}

public class FactoryContext : ChinookContext
{
    public FactoryContext(DbContextOptions<FactoryContext> options) : base(options) { }
}

public class PooledContext : ChinookContext
{
    public PooledContext(DbContextOptions<PooledContext> options) : base(options) { }
}
