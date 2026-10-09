using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Dapper;
using ef_vs_dapper_vs_ado.Chinook;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ef_vs_dapper_vs_ado;

// Same hot-path read (look up a Track by Name) across three approaches.
// Spec uses "Book by Isbn" - adapted to Chinook (Track by Name).
//
// Like-for-like rules:
//  * Same semantics: EF's FirstOrDefault emits "LIMIT 1", so Dapper uses QueryFirstOrDefault and
//    Dapper/ADO.NET run the same SQL with "LIMIT 1" (QuerySingleOrDefault would read a 2nd row to
//    check uniqueness - different work).
//  * Same columns: all three select and materialize every Track column.
//  * Same connection handling: all three use ONE connection opened once up front. EF normally
//    opens/closes its connection around every query; Database.OpenConnection() keeps it open, so the
//    benchmark measures query + materialization overhead, not connection open/close.

public static class Program
{
    public const string ConnectionString = "Data Source=chinook.db";
    public const string TrackName = "Inject The Venom";

    public const string TrackSql =
        "SELECT Id, Name, AlbumId, MediaTypeId, GenreId, Composer, Milliseconds, Bytes, UnitPrice FROM Track WHERE Name = @name LIMIT 1";

    public static void Main(string[] args)
    {
        // One smoke pass so the audience can see the same row coming back from all three.
        using (var db = new ChinookContext())
        {
            var ef = db.Tracks.AsNoTracking().FirstOrDefault(t => t.Name == TrackName);
            Console.WriteLine($"[EF Core ] Track #{ef?.Id} '{ef?.Name}' on Album {ef?.AlbumId}");
        }

        using (var c = new SqliteConnection(ConnectionString))
        {
            c.Open();
            var d = c.QueryFirstOrDefault<Track>(TrackSql, new { name = TrackName });
            Console.WriteLine($"[Dapper  ] Track #{d?.Id} '{d?.Name}' on Album {d?.AlbumId}");
        }

        using (var c = new SqliteConnection(ConnectionString))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = TrackSql;
            cmd.Parameters.AddWithValue("@name", TrackName);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Console.WriteLine($"[ADO.NET ] Track #{reader.GetInt32(0)} '{reader.GetString(1)}' on Album {reader.GetInt32(2)}");
            }
        }

        if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<ReadBenchmarks>();
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");
    }
}

[MemoryDiagnoser]
public class ReadBenchmarks
{
    private ChinookContext _db = null!;
    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public void Setup()
    {
        _db = new ChinookContext();
        _db.Database.OpenConnection(); // keep EF's connection open, same as the Dapper/ADO.NET one below
        _db.Tracks.AsNoTracking().FirstOrDefault(); // warm up EF model + query cache

        _connection = new SqliteConnection(Program.ConnectionString);
        _connection.Open();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Track EfCore()
        => _db.Tracks.AsNoTracking().FirstOrDefault(t => t.Name == Program.TrackName);

    [Benchmark]
    public Track Dapper()
        => _connection.QueryFirstOrDefault<Track>(Program.TrackSql, new { name = Program.TrackName });

    [Benchmark]
    public Track Ado()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = Program.TrackSql;
        cmd.Parameters.AddWithValue("@name", Program.TrackName);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Track
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            AlbumId = reader.GetInt32(2),
            MediaTypeId = reader.GetInt32(3),
            GenreId = reader.GetInt32(4),
            Composer = reader.IsDBNull(5) ? null : reader.GetString(5),
            Milliseconds = reader.GetInt32(6),
            Bytes = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
            UnitPrice = reader.GetDecimal(8)
        };
    }
}
