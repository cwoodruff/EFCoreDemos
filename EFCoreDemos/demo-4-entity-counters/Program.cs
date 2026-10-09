using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Demos.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demos;

public class GenreController
{
    private readonly ChinookContext _context;

    public GenreController(ChinookContext context) => _context = context;

    public async Task ActionAsync() => await _context.Genres.FirstAsync();
}

public class Startup
{
    private const string ConnectionString
        = @"Data Source=chinook.db";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextPool<ChinookContext>(c => c
            .UseSqlite(ConnectionString)
            .EnableSensitiveDataLogging());
    }
}

public class Program
{
    private const int Threads = 32;
    private const int Seconds = 10;

    private static long _requestsProcessed;

    private static async Task Main()
    {
        var serviceCollection = new ServiceCollection();
        new Startup().ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Each worker loops synchronously on its thread (SQLite I/O never yields), so make
        // sure the thread pool has enough threads up front instead of injecting ~1-2/sec.
        ThreadPool.GetMinThreads(out _, out var minIo);
        ThreadPool.SetMinThreads(Threads + Environment.ProcessorCount, minIo);

        var stopwatch = new Stopwatch();

        // Starts the stopwatch synchronously, then reports once per second.
        var monitor = MonitorResultsAsync(TimeSpan.FromSeconds(Seconds), stopwatch);

        // Task.Run puts each simulated "request loop" on its own thread-pool thread.
        // Without it the workers would NOT run concurrently: SQLite's async methods
        // complete synchronously, so the first worker would loop on the main thread
        // until time ran out and only a single pooled context would ever be rented.
        var workers = Enumerable
            .Range(0, Threads)
            .Select(_ => Task.Run(() => SimulateRequestsAsync(serviceProvider, stopwatch)))
            .ToArray();

        await monitor;
        stopwatch.Stop();
        await Task.WhenAll(workers);

        Console.WriteLine();
        Console.WriteLine($"Concurrent workers:      {Threads}");
        Console.WriteLine($"Total context creations: {ChinookContext.InstanceCount}  (pooling: at most one per concurrent request, then reused)");
        Console.WriteLine($"Total requests:          {Interlocked.Read(ref _requestsProcessed)}");
        Console.WriteLine(
            $"Requests per second:     {Math.Round(_requestsProcessed / stopwatch.Elapsed.TotalSeconds)}");

        // Pause only when running interactively; ReadKey throws when input is redirected.
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    private static async Task SimulateRequestsAsync(IServiceProvider serviceProvider, Stopwatch stopwatch)
    {
        while (stopwatch.IsRunning)
        {
            using (var serviceScope = serviceProvider.CreateScope())
            {
                await new GenreController(serviceScope.ServiceProvider.GetService<ChinookContext>()).ActionAsync();
            }

            Interlocked.Increment(ref _requestsProcessed);
        }
    }

    private static async Task MonitorResultsAsync(TimeSpan duration, Stopwatch stopwatch)
    {
        var lastInstanceCount = 0L;
        var lastRequestCount = 0L;
        var lastElapsed = TimeSpan.Zero;

        stopwatch.Start();

        while (stopwatch.Elapsed < duration)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            var instanceCount = Interlocked.Read(ref ChinookContext.InstanceCount);
            var requestCount = Interlocked.Read(ref _requestsProcessed);
            var elapsed = stopwatch.Elapsed;
            var currentElapsed = elapsed - lastElapsed;
            var currentRequests = requestCount - lastRequestCount;

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] "
                + $"Context creations/second: {instanceCount - lastInstanceCount} | "
                + $"Requests/second: {Math.Round(currentRequests / currentElapsed.TotalSeconds)}");

            lastInstanceCount = instanceCount;
            lastRequestCount = requestCount;
            lastElapsed = elapsed;
        }
    }
}
