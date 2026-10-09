extern alias interception_db_ops;

using System.Data.Common;
using EFCoreDemos.Tests.Infrastructure;
using interception_db_ops::interception_db_ops.Chinook;
using interception_db_ops::interception_db_ops.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// interception-db-ops: the HintCommandInterceptor registered in ChinookContext.OnConfiguring rewrites the
    /// command text of every reader command (sync and async) before it reaches the database - a leading SQL
    /// comment on SQLite, an OPTION (...) query hint on SQL Server - and the rewritten SQL still executes.
    /// </summary>
    public class InterceptionDbOpsTests : IDisposable
    {
        private const string SqliteMarker = "-- Rewritten by HintCommandInterceptor";
        private const int RockTrackCount = 1297; // GenreId == 1

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        /// <summary>
        /// Keeps the demo's own OnConfiguring (SQLite + HintCommandInterceptor) and appends a capturing
        /// interceptor after it, so the capture sees the command text exactly as the database receives it.
        /// </summary>
        private sealed class ObservedChinookContext(CommandCapture capture) : ChinookContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                base.OnConfiguring(optionsBuilder);
                optionsBuilder.AddInterceptors(capture);
            }
        }

        private sealed class SqlServerHintContext(CommandCapture capture) : ChinookContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
                optionsBuilder
                    .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=master;")
                    .AddInterceptors(new HintCommandInterceptor(), capture);
        }

        private sealed class CommandCapture : DbCommandInterceptor
        {
            private readonly List<string> _commands = [];

            public IReadOnlyList<string> Commands
            {
                get { lock (_commands) return _commands.ToList(); }
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            {
                Record(command);
                return result;
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Record(command);
                return new ValueTask<InterceptionResult<DbDataReader>>(result);
            }

            private void Record(DbCommand command)
            {
                lock (_commands) _commands.Add(command.CommandText);
            }
        }

        private ObservedChinookContext CreateContext(CommandCapture capture) => _db.Redirect(new ObservedChinookContext(capture));

        [Fact]
        public async Task AsyncQuery_CommandTextIsRewritten_AndStillReturnsTheTracks()
        {
            var capture = new CommandCapture();
            using var db = CreateContext(capture);

            // Same query the demo runs (async path -> ReaderExecutingAsync).
            var names = new List<string>();
            await foreach (var track in db.Tracks.Where(t => t.GenreId == 1).AsAsyncEnumerable())
            {
                names.Add(track.Name);
            }

            Assert.Equal(RockTrackCount, names.Count);
            var sql = Assert.Single(capture.Commands);
            Assert.StartsWith(SqliteMarker, sql);
            Assert.Contains("\"GenreId\"", sql);
        }

        [Fact]
        public void SyncQuery_GoesThroughReaderExecuting_AndIsRewrittenToo()
        {
            var capture = new CommandCapture();
            using var db = CreateContext(capture);

            var count = db.Tracks.Count(t => t.GenreId == 1);
            var albums = db.Albums.OrderBy(a => a.Id).Take(3).ToList();

            Assert.Equal(RockTrackCount, count);
            Assert.Equal(3, albums.Count);
            Assert.Equal(2, capture.Commands.Count);
            Assert.All(capture.Commands, sql => Assert.StartsWith(SqliteMarker, sql));
        }

        [Fact]
        public void EveryCommandIsRewrittenExactlyOnce()
        {
            var capture = new CommandCapture();
            using var db = CreateContext(capture);

            _ = db.Genres.ToList();
            _ = db.Genres.ToList();

            Assert.Equal(2, capture.Commands.Count);
            Assert.All(capture.Commands, sql =>
            {
                var first = sql.IndexOf(SqliteMarker, StringComparison.Ordinal);
                Assert.Equal(0, first);
                Assert.Equal(-1, sql.IndexOf(SqliteMarker, first + 1, StringComparison.Ordinal));
            });
        }

        [Fact]
        public void DemoContext_RegistersHintCommandInterceptor_InOnConfiguring()
        {
            using var db = _db.Redirect(new ChinookContext());

            // Without the capture interceptor the rewritten query still runs fine against SQLite.
            Assert.Equal(RockTrackCount, db.Tracks.Count(t => t.GenreId == 1));

            var interceptors = db.GetService<IDbContextOptions>()
                .FindExtension<CoreOptionsExtension>()!
                .Interceptors;
            Assert.NotNull(interceptors);
            Assert.Contains(interceptors, i => i is HintCommandInterceptor);
        }

        [SqlServerFact]
        public void SqlServer_AppendsOptimizeForUnknownHint()
        {
            var capture = new CommandCapture();
            using var db = new SqlServerHintContext(capture);

            var result = db.Database.SqlQueryRaw<int>("SELECT 1 AS [Value]").ToList();

            Assert.Equal(1, Assert.Single(result));
            var sql = Assert.Single(capture.Commands);
            Assert.EndsWith(" OPTION (OPTIMIZE FOR UNKNOWN)", sql);
            Assert.DoesNotContain(SqliteMarker, sql);
        }
    }
}
