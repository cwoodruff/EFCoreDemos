extern alias simple_logging_improved_diagnostics;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using simple_logging_improved_diagnostics::simple_logging_improved_diagnostics.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// simple-logging-improved-diagnostics: LogTo writes EF's log events (filterable by category or EventId),
    /// EnableSensitiveDataLogging puts parameter values into those logs, TagWith shows up as a SQL comment, and
    /// the demo's split-query Include issues one command per collection.
    /// </summary>
    public class SimpleLoggingImprovedDiagnosticsTests : IDisposable
    {
        private const string DemoTag = "simple-logging-improved-diagnostics: Artists with Albums (split query)";

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(Action<DbContextOptionsBuilder<ChinookContext>> configure)
        {
            var builder = new DbContextOptionsBuilder<ChinookContext>().UseSqlite(_db.ConnectionString);
            configure(builder);
            return new ChinookContext(builder.Options);
        }

        private static List<Artist> RunDemoQuery(ChinookContext db) =>
            db.Artists
                .TagWith(DemoTag)
                .Include(e => e.Albums)
                .AsSplitQuery()
                .OrderBy(e => e.Id)
                .Take(5)
                .ToList();

        [Fact]
        public void DefaultConstructor_ConfiguresSqliteWithSensitiveDataLogging()
        {
            using var db = new ChinookContext();

            var core = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();

            Assert.True(db.Database.IsSqlite());
            Assert.NotNull(core);
            Assert.True(core.IsSensitiveDataLoggingEnabled);
            Assert.NotNull(core.LoggerFactory);
        }

        [Fact]
        public void DemoQuery_SplitQueryWithTag_LogsTwoTaggedCommands()
        {
            var log = new SqlLog();
            using var db = CreateContext(b => b.LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information));

            var artists = RunDemoQuery(db);

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, artists.Select(a => a.Id).ToArray());
            Assert.Equal(new[] { 2, 2, 1, 1, 1 }, artists.Select(a => a.Albums.Count).ToArray());

            var commands = log.Commands;
            Assert.Equal(2, commands.Count);
            Assert.Contains("-- " + DemoTag, commands[0]);
            Assert.Contains("\"Album\"", commands[1]);
        }

        [Fact]
        public void EnableSensitiveDataLogging_ShowsParameterValues()
        {
            var log = new SqlLog();
            using var db = CreateContext(b => b
                .LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .EnableSensitiveDataLogging());

            var name = "AC/DC";
            var artist = db.Artists.Single(a => a.Name == name);

            Assert.Equal(1, artist.Id);
            var command = Assert.Single(log.Commands);
            Assert.Contains("='AC/DC'", command);
        }

        [Fact]
        public void WithoutSensitiveDataLogging_ParameterValuesAreMasked()
        {
            var log = new SqlLog();
            using var db = CreateContext(b => b.LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information));

            var name = "AC/DC";
            _ = db.Artists.Single(a => a.Name == name);

            var command = Assert.Single(log.Commands);
            Assert.Contains("='?'", command);
            Assert.DoesNotContain("AC/DC", command);
        }

        [Fact]
        public void LogTo_FilteredByEventId_OnlyEmitsThatEvent()
        {
            var entries = new List<string>();
            using var db = CreateContext(b => b.LogTo(entries.Add, new[] { RelationalEventId.CommandExecuted }));

            _ = RunDemoQuery(db);

            Assert.Equal(2, entries.Count);
            Assert.All(entries, e =>
            {
                Assert.Contains("Executed DbCommand", e);
                Assert.Contains(nameof(RelationalEventId.CommandExecuted), e);
            });
        }

        [Fact]
        public void LogTo_WithDefaultLevel_IncludesConnectionAndQueryCompilationEvents()
        {
            var entries = new List<string>();
            using var db = CreateContext(b => b.LogTo(entries.Add, LogLevel.Debug));

            _ = RunDemoQuery(db);

            Assert.Contains(entries, e => e.Contains(nameof(RelationalEventId.ConnectionOpened)));
            Assert.Contains(entries, e => e.Contains(nameof(CoreEventId.QueryExecutionPlanned)));
            Assert.Equal(2, entries.Count(e => e.Contains("Executed DbCommand")));
        }
    }
}
