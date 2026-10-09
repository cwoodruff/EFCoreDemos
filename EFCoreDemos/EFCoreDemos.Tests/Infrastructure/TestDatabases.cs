using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EFCoreDemos.Tests.Infrastructure
{
    /// <summary>
    /// A private, throwaway copy of the seeded Chinook SQLite database.
    /// Tests that write (SaveChanges, ExecuteUpdate, ...) use one of these so the shared chinook.db in the
    /// output folder is never modified and test classes can run in parallel.
    /// </summary>
    public sealed class ChinookCopy : IDisposable
    {
        private static readonly string Seed = System.IO.Path.Combine(AppContext.BaseDirectory, "chinook.db");

        public ChinookCopy()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"chinook-test-{Guid.NewGuid():N}.db");
            File.Copy(Seed, Path);
        }

        public string Path { get; }

        // Pooling off so the file handle is released and the copy can be deleted on Dispose.
        public string ConnectionString => $"Data Source={Path};Pooling=False";

        /// <summary>Options for a context that has a DbContextOptions constructor.</summary>
        public DbContextOptions<T> Options<T>(SqlLog? log = null) where T : DbContext
        {
            var builder = new DbContextOptionsBuilder<T>().UseSqlite(ConnectionString);
            if (log != null)
            {
                builder.LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
            }

            return builder.Options;
        }

        /// <summary>
        /// Points a context that only has a parameterless constructor (connection string hard-coded in
        /// OnConfiguring) at this copy. Must be called before the context first opens a connection.
        /// </summary>
        public T Redirect<T>(T context) where T : DbContext
        {
            context.Database.SetConnectionString(ConnectionString);
            return context;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(Path); } catch (IOException) { }
        }
    }

    /// <summary>A unique temp file path for demos that create their own SQLite database from scratch.</summary>
    public sealed class TempSqliteFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"efdemo-test-{Guid.NewGuid():N}.db");

        public string ConnectionString => $"Data Source={Path};Pooling=False";

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(Path); } catch (IOException) { }
        }
    }

    /// <summary>Collects the SQL commands EF executes (wire up with <c>LogTo(log.Add, ...)</c>).</summary>
    public sealed class SqlLog
    {
        private readonly List<string> _entries = [];

        public void Add(string message)
        {
            lock (_entries) _entries.Add(message);
        }

        public IReadOnlyList<string> Entries
        {
            get { lock (_entries) return _entries.ToList(); }
        }

        /// <summary>Executed DbCommand log entries (one per round trip).</summary>
        public IReadOnlyList<string> Commands => Entries.Where(e => e.Contains("Executed DbCommand")).ToList();

        public void Clear()
        {
            lock (_entries) _entries.Clear();
        }
    }

    /// <summary>The docker SQL Server (container sql2025) used by the SQL Server demos.</summary>
    public static class SqlServerTestServer
    {
        public const string ConnectionString =
            "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;TrustServerCertificate=True;Connect Timeout=10;";

        private static readonly Lazy<bool> Reachable = new(() =>
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("localhost", 1433).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
            }
            catch
            {
                return false;
            }
        });

        public static bool IsAvailable => Reachable.Value;
    }

    /// <summary>A fact that is skipped (not failed) when the docker SQL Server is not running.</summary>
    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (!SqlServerTestServer.IsAvailable)
            {
                Skip = "SQL Server on localhost:1433 is not reachable (start the sql2025 docker container).";
            }
        }
    }
}
