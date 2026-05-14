using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using simple_logging_improved_diagnostics.Chinook;

namespace simple_logging_improved_diagnostics;

class Program
{
    private const string ConnectionString = "Data Source=chinook.db";

    static void Main()
    {
        DemoLoggingAndDiagnostics();
        Console.WriteLine();
        DemoWhenNotToUseEfCore();
    }

    // The original demo: show LogTo + EnableSensitiveDataLogging output for
    // a split-query Include against the Artist/Album graph.
    private static void DemoLoggingAndDiagnostics()
    {
        Console.WriteLine("== Simple logging + improved diagnostics ==");
        using var db = new ChinookContext();

        var artists = db.Artists
            .TagWith("simple-logging-improved-diagnostics: Artists with Albums (split query)")
            .Include(e => e.Albums)
            .AsSplitQuery()
            .Take(5)
            .ToList();

        foreach (var artist in artists)
        {
            Console.WriteLine($"  Artist: {artist.Name} ({artist.Albums.Count} albums)");
        }
    }

    // The "when not to use EF Core" comparison: same single-row read across
    // EF Core, Dapper, and raw ADO.NET. Numbers are noisy on a console run,
    // but the relative ordering and allocation difference is the teaching point.
    private static void DemoWhenNotToUseEfCore()
    {
        Console.WriteLine("== When not to use EF Core: same read, three ways ==");

        const int iterations = 200;
        const int trackId = 1;

        // Warm everything up so JIT and connection-pool costs don't pollute the comparison.
        WarmUp();

        var efMs = Time(iterations, () =>
        {
            using var db = new ChinookContext();
            _ = db.Tracks.AsNoTracking().FirstOrDefault(t => t.Id == trackId);
        });

        var dapperMs = Time(iterations, () =>
        {
            using var c = new SqliteConnection(ConnectionString);
            c.Open();
            _ = c.QuerySingleOrDefault<TrackRow>(
                "SELECT Id, Name, AlbumId FROM Track WHERE Id = @id",
                new { id = trackId });
        });

        var adoMs = Time(iterations, () =>
        {
            using var c = new SqliteConnection(ConnectionString);
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, AlbumId FROM Track WHERE Id = @id";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = trackId;
            cmd.Parameters.Add(p);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                _ = new TrackRow
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    AlbumId = reader.GetInt32(2)
                };
            }
        });

        Console.WriteLine($"  {iterations,4} x EF Core   FirstOrDefault: {efMs,7:F2} ms");
        Console.WriteLine($"  {iterations,4} x Dapper    QuerySingle:    {dapperMs,7:F2} ms");
        Console.WriteLine($"  {iterations,4} x ADO.NET   reader:         {adoMs,7:F2} ms");
        Console.WriteLine();
        Console.WriteLine("  EF Core's overhead is ~constant per call. At hundreds of requests per second");
        Console.WriteLine("  it disappears under the wire latency. On tight inner loops it earns the rewrite.");
    }

    private static void WarmUp()
    {
        using var db = new ChinookContext();
        _ = db.Tracks.AsNoTracking().FirstOrDefault();

        using var c = new SqliteConnection(ConnectionString);
        c.Open();
        _ = c.QuerySingleOrDefault<TrackRow>("SELECT Id, Name, AlbumId FROM Track WHERE Id = 1");
    }

    private static double Time(int iterations, Action body)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) body();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private sealed class TrackRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int AlbumId { get; set; }
    }
}
