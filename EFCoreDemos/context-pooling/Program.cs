using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using context_pooling.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace context_pooling;

public class GenreController(ChinookContext? context)
{
    public async Task ActionAsync() => await context.Genres.FirstAsync();
}

public class Startup
{
    private const string ConnectionString
        = @"Data Source=chinook.db";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextPool<ChinookContext>(c => c.UseSqlite(ConnectionString));
        //services.AddDbContext<ChinookContext>(c => c.UseSqlServer(ConnectionString));
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

        var stopwatch = new Stopwatch();

        MonitorResults(TimeSpan.FromSeconds(Seconds), stopwatch);

        await Task.WhenAll(
            Enumerable
                .Range(0, Threads)
                .Select(_ => SimulateRequestsAsync(serviceProvider, stopwatch)));

        Console.ReadLine();
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

    private static async void MonitorResults(TimeSpan duration, Stopwatch stopwatch)
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
                $"[{DateTime.Now:HH:mm:ss.fff}] "
                + $"Context creations/second: {instanceCount - lastInstanceCount} | "
                + $"Requests/second: {Math.Round(currentRequests / currentElapsed.TotalSeconds)}");

            lastInstanceCount = instanceCount;
            lastRequestCount = requestCount;
            lastElapsed = elapsed;
        }

        Console.WriteLine();
        Console.WriteLine($"Total context creations: {ChinookContext.InstanceCount}");
        Console.WriteLine(
            $"Requests per second:     {Math.Round(_requestsProcessed / stopwatch.Elapsed.TotalSeconds)}");

        stopwatch.Stop();
    }
}