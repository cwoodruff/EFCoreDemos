extern alias temporal_tables;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using temporal_tables::temporal_tables.TemporalDemo;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// temporal-tables: IsTemporal() maps Employees to a SQL Server system-versioned table; updates and
    /// deletes move old row versions to EmployeeHistory, and TemporalAll/AsOf/Between query that history.
    /// Each SQL Server test creates (and drops) its own uniquely-named database.
    /// </summary>
    public class TemporalTablesTests
    {
        private static TemporalContext CreateContext(string databaseName)
        {
            // TemporalContext only has a parameterless constructor; redirect it before it opens a connection.
            var db = new TemporalContext();
            db.Database.SetConnectionString(SqlServerTestServer.ConnectionString + $"Database={databaseName};");
            return db;
        }

        private static string NewDatabaseName() => $"TemporalTablesTest_{Guid.NewGuid():N}";

        // Period columns hold the transaction (server UTC) time, so pause and read the server clock
        // to get points in time that fall cleanly between changes.
        private static async Task<DateTime> CheckpointAsync(string databaseName)
        {
            await Task.Delay(300);
            await using var db = CreateContext(databaseName);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync();
            await Task.Delay(300);
            return now;
        }

        private static async Task SeedAsync(string databaseName)
        {
            await using var db = CreateContext(databaseName);
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            db.Employees.AddRange(
                new Employee { Id = 1, Name = "Andrew Adams", Position = "General Manager", Salary = 120_000m },
                new Employee { Id = 2, Name = "Nancy Edwards", Position = "Sales Manager", Salary = 95_000m },
                new Employee { Id = 3, Name = "Jane Peacock", Position = "Sales Support Agent", Salary = 60_000m });
            await db.SaveChangesAsync();
        }

        private static async Task DropAsync(string databaseName)
        {
            await using var db = CreateContext(databaseName);
            await db.Database.EnsureDeletedAsync();
        }

        [Fact]
        public void Model_EmployeesIsTemporalWithHistoryTableAndPeriodColumns()
        {
            using var db = new TemporalContext();
            // Temporal configuration lives in the design-time model, not the read-optimized runtime model.
            var employee = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Employee))!;

            Assert.True(employee.IsTemporal());
            Assert.Equal("EmployeeHistory", employee.GetHistoryTableName());
            Assert.Equal("ValidFrom", employee.GetPeriodStartPropertyName());
            Assert.Equal("ValidTo", employee.GetPeriodEndPropertyName());

            // Period columns are shadow properties.
            Assert.True(employee.FindProperty("ValidFrom")!.IsShadowProperty());
            Assert.True(employee.FindProperty("ValidTo")!.IsShadowProperty());
        }

        [Fact]
        public void TemporalOperators_TranslateToForSystemTime()
        {
            using var db = new TemporalContext();
            var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.Contains("FOR SYSTEM_TIME ALL", db.Employees.TemporalAll().ToQueryString());
            Assert.Contains("FOR SYSTEM_TIME AS OF", db.Employees.TemporalAsOf(at).ToQueryString());
            Assert.Contains("FOR SYSTEM_TIME BETWEEN", db.Employees.TemporalBetween(at, at.AddDays(1)).ToQueryString());
        }

        [SqlServerFact]
        public async Task Update_KeepsOldVersion_VisibleThroughTemporalAllAsOfAndBetween()
        {
            var dbName = NewDatabaseName();
            try
            {
                await SeedAsync(dbName);
                var afterInsert = await CheckpointAsync(dbName);

                await using (var db = CreateContext(dbName))
                {
                    var jane = await db.Employees.SingleAsync(e => e.Id == 3);
                    jane.Position = "Sales Manager";
                    jane.Salary = 85_000m;
                    await db.SaveChangesAsync();
                }

                var afterPromotion = await CheckpointAsync(dbName);

                await using (var db = CreateContext(dbName))
                {
                    // Regular query: only the current version.
                    var current = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == 3);
                    Assert.Equal("Sales Manager", current.Position);

                    // TemporalAll: both versions of Jane, the old one closed, the new one open-ended.
                    var janeHistory = await db.Employees
                        .TemporalAll()
                        .Where(e => e.Id == 3)
                        .OrderBy(e => EF.Property<DateTime>(e, "ValidFrom"))
                        .Select(e => new { e.Position, e.Salary, ValidTo = EF.Property<DateTime>(e, "ValidTo") })
                        .ToListAsync();

                    Assert.Equal(2, janeHistory.Count);
                    Assert.Equal("Sales Support Agent", janeHistory[0].Position);
                    Assert.Equal(60_000m, janeHistory[0].Salary);
                    Assert.True(janeHistory[0].ValidTo < DateTime.MaxValue.AddYears(-1));
                    Assert.Equal("Sales Manager", janeHistory[1].Position);
                    Assert.Equal(85_000m, janeHistory[1].Salary);
                    Assert.True(janeHistory[1].ValidTo > afterPromotion.AddYears(1));

                    // Unchanged rows have a single version.
                    Assert.Equal(4, await db.Employees.TemporalAll().CountAsync());

                    // TemporalAsOf: the table as it was right after the insert.
                    var snapshot = await db.Employees.TemporalAsOf(afterInsert).AsNoTracking().OrderBy(e => e.Id).ToListAsync();
                    Assert.Equal(new[] { 1, 2, 3 }, snapshot.Select(e => e.Id));
                    Assert.Equal("Sales Support Agent", snapshot.Single(e => e.Id == 3).Position);

                    var later = await db.Employees.TemporalAsOf(afterPromotion).AsNoTracking().SingleAsync(e => e.Id == 3);
                    Assert.Equal("Sales Manager", later.Position);

                    // TemporalBetween: every version of Jane active in the window.
                    var between = await db.Employees
                        .TemporalBetween(afterInsert, afterPromotion)
                        .Where(e => e.Id == 3)
                        .Select(e => e.Position)
                        .ToListAsync();
                    Assert.Equal(2, between.Count);
                    Assert.Contains("Sales Support Agent", between);
                    Assert.Contains("Sales Manager", between);
                }
            }
            finally
            {
                await DropAsync(dbName);
            }
        }

        [SqlServerFact]
        public async Task Delete_RowLivesOnInHistory_AndCanBeRestored()
        {
            var dbName = NewDatabaseName();
            try
            {
                await SeedAsync(dbName);
                var beforeDelete = await CheckpointAsync(dbName);

                await using (var db = CreateContext(dbName))
                {
                    var nancy = await db.Employees.SingleAsync(e => e.Id == 2);
                    db.Employees.Remove(nancy);
                    await db.SaveChangesAsync();
                }

                await CheckpointAsync(dbName);

                await using (var db = CreateContext(dbName))
                {
                    Assert.False(await db.Employees.AnyAsync(e => e.Id == 2));
                    Assert.Equal(1, await db.Employees.TemporalAll().CountAsync(e => e.Id == 2));

                    // Restore from history, exactly like the demo.
                    var deleted = await db.Employees.TemporalAsOf(beforeDelete).AsNoTracking().SingleAsync(e => e.Id == 2);
                    Assert.Equal("Nancy Edwards", deleted.Name);

                    db.Employees.Add(new Employee
                    {
                        Id = deleted.Id,
                        Name = deleted.Name,
                        Position = deleted.Position,
                        Salary = deleted.Salary
                    });
                    await db.SaveChangesAsync();
                }

                await using (var db = CreateContext(dbName))
                {
                    var restored = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == 2);
                    Assert.Equal("Nancy Edwards", restored.Name);
                    Assert.Equal("Sales Manager", restored.Position);
                    Assert.Equal(95_000m, restored.Salary);
                    Assert.Equal(3, await db.Employees.CountAsync());

                    // The original (deleted) version plus the restored one.
                    Assert.Equal(2, await db.Employees.TemporalAll().CountAsync(e => e.Id == 2));
                }
            }
            finally
            {
                await DropAsync(dbName);
            }
        }
    }
}
