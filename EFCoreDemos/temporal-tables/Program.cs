using Microsoft.EntityFrameworkCore;
using temporal_tables.TemporalDemo;

namespace temporal_tables;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // 1. Create the database. The CREATE TABLE shows PERIOD FOR SYSTEM_TIME + SYSTEM_VERSIONING.
        Header("1. Create a temporal table (SQL Server, database TemporalTablesDemo)");
        await using (var db = new TemporalContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        // 2. Insert rows. Normal EF code: temporal behavior is entirely on the server.
        Header("2. Insert employees");
        await using (var db = new TemporalContext())
        {
            db.Employees.AddRange(
                new Employee { Id = 1, Name = "Andrew Adams", Position = "General Manager", Salary = 120_000m },
                new Employee { Id = 2, Name = "Nancy Edwards", Position = "Sales Manager", Salary = 95_000m },
                new Employee { Id = 3, Name = "Jane Peacock", Position = "Sales Support Agent", Salary = 60_000m });
            await db.SaveChangesAsync();
        }

        // Period columns hold the transaction time, so pause between changes
        // and remember the server's clock to have distinct points in time to query.
        var afterInsert = await CheckpointAsync("after insert");

        // 3. Update: the old version of the row is moved to EmployeeHistory.
        Header("3. Promote Jane (update)");
        await using (var db = new TemporalContext())
        {
            var jane = await db.Employees.SingleAsync(e => e.Id == 3);
            jane.Position = "Sales Manager";
            jane.Salary = 85_000m;
            await db.SaveChangesAsync();
        }

        var afterPromotion = await CheckpointAsync("after promotion");

        // 4. Delete: the row disappears from Employees but lives on in history.
        Header("4. Nancy leaves (delete)");
        await using (var db = new TemporalContext())
        {
            var nancy = await db.Employees.SingleAsync(e => e.Id == 2);
            db.Employees.Remove(nancy);
            await db.SaveChangesAsync();
        }

        var afterDelete = await CheckpointAsync("after delete");

        // 5. Normal query: only current rows.
        Header("5. Current state (regular query)");
        await using (var db = new TemporalContext())
        {
            var current = await db.Employees.OrderBy(e => e.Id).ToListAsync();
            foreach (var e in current)
            {
                Console.WriteLine($"  {e.Id} {e.Name,-15} {e.Position,-20} {e.Salary,10:C0}");
            }
        }

        // 6. TemporalAll: every version of every row, with the period columns.
        Header("6. TemporalAll - full history (FOR SYSTEM_TIME ALL)");
        await using (var db = new TemporalContext())
        {
            var history = await db.Employees
                .TemporalAll()
                .OrderBy(e => e.Id)
                .ThenBy(e => EF.Property<DateTime>(e, "ValidFrom"))
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Position,
                    e.Salary,
                    ValidFrom = EF.Property<DateTime>(e, "ValidFrom"),
                    ValidTo = EF.Property<DateTime>(e, "ValidTo")
                })
                .ToListAsync();

            foreach (var h in history)
            {
                var to = h.ValidTo == DateTime.MaxValue ? "(current)" : h.ValidTo.ToString("HH:mm:ss.fff");
                Console.WriteLine($"  {h.Id} {h.Name,-15} {h.Position,-20} {h.Salary,10:C0}  {h.ValidFrom:HH:mm:ss.fff} -> {to}");
            }
        }

        // 7. TemporalAsOf: the table exactly as it looked at a point in time.
        Header("7. TemporalAsOf - the table as it was right after the insert");
        await using (var db = new TemporalContext())
        {
            var snapshot = await db.Employees
                .TemporalAsOf(afterInsert)
                .OrderBy(e => e.Id)
                .ToListAsync();

            foreach (var e in snapshot)
            {
                Console.WriteLine($"  {e.Id} {e.Name,-15} {e.Position,-20} {e.Salary,10:C0}");
            }
        }

        // 8. TemporalBetween: versions of one row that were active in a time range.
        Header("8. TemporalBetween - Jane's versions between insert and delete checkpoints");
        await using (var db = new TemporalContext())
        {
            var janeVersions = await db.Employees
                .TemporalBetween(afterInsert, afterDelete)
                .Where(e => e.Id == 3)
                .OrderBy(e => EF.Property<DateTime>(e, "ValidFrom"))
                .Select(e => new { e.Position, e.Salary, ValidFrom = EF.Property<DateTime>(e, "ValidFrom") })
                .ToListAsync();

            foreach (var v in janeVersions)
            {
                Console.WriteLine($"  {v.ValidFrom:HH:mm:ss.fff}  {v.Position,-20} {v.Salary,10:C0}");
            }
        }

        // 9. Restore the deleted row: read it from history and insert it again.
        Header("9. Restore Nancy from history");
        await using (var db = new TemporalContext())
        {
            var deleted = await db.Employees
                .TemporalAsOf(afterPromotion)
                .AsNoTracking()
                .SingleAsync(e => e.Id == 2);

            db.Employees.Add(new Employee
            {
                Id = deleted.Id,
                Name = deleted.Name,
                Position = deleted.Position,
                Salary = deleted.Salary
            });
            await db.SaveChangesAsync();

            var restored = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == 2);
            Console.WriteLine($"  Restored: {restored.Id} {restored.Name} ({restored.Position})");
        }
    }

    // Waits a moment, then reads the SQL Server clock (UTC, same clock as the period columns).
    private static async Task<DateTime> CheckpointAsync(string label)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        await using var db = new TemporalContext();
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));
        Console.WriteLine($"  -- checkpoint {label}: {now:HH:mm:ss.fff} UTC");
        return now;
    }

    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 70));
    }
}
