using System.Diagnostics;
using BenchmarkDotNet.Running;
using bulk_insert_perf;
using bulk_insert_perf.Benchmarks;
using bulk_insert_perf.Data;

// Default: show what EF sends for a bulk insert and time each approach once.
// Benchmark:  dotnet run -c Release -- benchmark
if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
{
    BenchmarkRunner.Run<BulkInsertBenchmarks>(BulkInsertBenchmarks.CreateConfig(), args[1..]);
    return;
}

const int rowCount = 10_000;
const int perRowCap = 1_000;
var connectionString = BulkInsertBenchmarks.ConnectionString;

Console.WriteLine($"Database: BulkInsertDemo (created if missing), table dbo.OrderLines (IDENTITY key, 5 data columns)");
Console.WriteLine("The table is truncated before every run.");
await BulkContext.ResetAsync(connectionString);

// Warm up the model, the connection pool and every insert path so no timing pays for first use.
await BulkInserts.PerRowSaveChangesAsync(BulkContext.CreateOptions(connectionString), OrderLine.Generate(10));
await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(connectionString), OrderLine.Generate(100));
await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(connectionString, BulkInserts.TunedBatchSize), OrderLine.Generate(800));
await BulkInserts.SqlBulkCopyAsync(connectionString, OrderLine.Generate(100));

var counter = new CommandCounter();

Section($"1. What EF Core 10 sends for AddRange({rowCount:N0} rows) + one SaveChanges");
await BulkContext.TruncateAsync(connectionString);
await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(connectionString, counter: counter), OrderLine.Generate(rowCount));
var defaultCommands = counter.Commands;
var defaultRowsPerBatch = counter.FirstCommandParameterCount / 5;
Console.WriteLine($"Commands sent: {defaultCommands:N0} for {rowCount:N0} rows");
Console.WriteLine($"First command: {counter.FirstCommandParameterCount} parameters = {defaultRowsPerBatch} rows (5 parameters per row)");
Console.WriteLine();
Console.WriteLine(Shorten(counter.FirstCommandText!));
Console.WriteLine();
Console.WriteLine($"""
    One MERGE per batch: a multi-row insert whose OUTPUT clause reads back every new IDENTITY value,
    because EF must hand the generated Ids back to your objects. EF's default MaxBatchSize for SQL Server is
    {defaultRowsPerBatch} rows, so {rowCount:N0} rows take {defaultCommands:N0} round trips.
    """);

counter.Reset();
await BulkContext.TruncateAsync(connectionString);
await BulkInserts.AddRangeAsync(
    BulkContext.CreateOptions(connectionString, BulkInserts.TunedBatchSize, counter), OrderLine.Generate(rowCount));
Console.WriteLine();
Console.WriteLine($"With UseSqlServer(..., o => o.MaxBatchSize({BulkInserts.TunedBatchSize})): {counter.Commands:N0} commands, " +
                  $"up to {counter.MaxParameterCount:N0} parameters each.");
Console.WriteLine($"SQL Server caps a command at 2,100 parameters, so with 5 per row {BulkInserts.TunedBatchSize} rows is close to the ceiling.");

Section($"2. Five ways to insert the same rows ({rowCount:N0} rows; per-row SaveChanges capped at {perRowCap:N0})");
Console.WriteLine($"  {"Approach",-38} {"Rows",7} {"Commands",9} {"Time",9} {"Rows/sec",10} {"COUNT(*)",9}");

var defaultOptions = BulkContext.CreateOptions(connectionString, counter: counter);
var tunedOptions = BulkContext.CreateOptions(connectionString, BulkInserts.TunedBatchSize, counter);

var perRow = await MeasureAsync("Add + SaveChanges per row", perRowCap,
    rows => BulkInserts.PerRowSaveChangesAsync(defaultOptions, rows));
var addRange = await MeasureAsync("AddRange + SaveChanges (default batch)", rowCount,
    rows => BulkInserts.AddRangeAsync(defaultOptions, rows));
var tuned = await MeasureAsync($"AddRange + MaxBatchSize({BulkInserts.TunedBatchSize})", rowCount,
    rows => BulkInserts.AddRangeAsync(tunedOptions, rows));
await MeasureAsync("Add loop, AutoDetectChanges off", rowCount,
    rows => BulkInserts.AddWithoutDetectChangesAsync(defaultOptions, rows));
var bulkCopy = await MeasureAsync("SqlBulkCopy", rowCount,
    rows => BulkInserts.SqlBulkCopyAsync(connectionString, rows));

var perRowEstimate = perRow.TotalMilliseconds / perRowCap * rowCount;
Console.WriteLine();
Console.WriteLine($"""
    Per-row SaveChanges was stopped at {perRowCap:N0} rows: at that pace {rowCount:N0} rows would take about
    {perRowEstimate / 1000:N1} s ({perRowEstimate / addRange.TotalMilliseconds:N0}x the AddRange time). Each row is its own round trip and transaction.

    AddRange + one SaveChanges is the big win, and it is simply what SaveChanges does with many pending rows.

    A bigger MaxBatchSize is not a free speed-up: {BulkInserts.TunedBatchSize}-row MERGE statements cost SQL Server more per row
    than 42-row ones, so ~10x fewer round trips took {tuned.TotalMilliseconds:N0} ms vs {addRange.TotalMilliseconds:N0} ms here. The default of 42 is deliberate:
    change it only after measuring on your own network (a high-latency link shifts the balance).

    AutoDetectChanges off: no real change. EF Core's Add doesn't rescan the change tracker the way EF6 did.

    SqlBulkCopy: {addRange.TotalMilliseconds / bulkCopy.TotalMilliseconds:N0}x faster than AddRange. It skips EF, change tracking and reading back the
    generated Ids. Reach for it for imports and ETL, not for everyday saves.
    """);

await BulkContext.TruncateAsync(connectionString);
Console.WriteLine();
Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness (1k / 10k / 100k rows).");
return;

async Task<TimeSpan> MeasureAsync(string name, int rows, Func<List<OrderLine>, Task> insert)
{
    await BulkContext.TruncateAsync(connectionString);
    var data = OrderLine.Generate(rows);
    counter.Reset();

    var stopwatch = Stopwatch.StartNew();
    await insert(data);
    stopwatch.Stop();

    var count = await BulkContext.CountAsync(connectionString);
    var commands = name == "SqlBulkCopy" ? "(bulk)" : counter.Commands.ToString("N0");
    Console.WriteLine($"  {name,-38} {rows,7:N0} {commands,9} {stopwatch.Elapsed.TotalMilliseconds,6:N0} ms " +
                      $"{rows / stopwatch.Elapsed.TotalSeconds,10:N0} {count,9:N0}");
    return stopwatch.Elapsed;
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 100));
}

// A batch has one line per row; keep the first two and the last value rows so the shape stays readable.
static string Shorten(string sql)
{
    var lines = sql.Replace("\r\n", "\n").Split('\n');
    var rowLines = lines.Select((line, index) => (line, index))
        .Where(x => x.line.TrimStart().StartsWith("(@p") || x.line.TrimStart().StartsWith("VALUES (@p"))
        .Select(x => x.index)
        .ToList();

    if (rowLines.Count <= 4) return sql;

    var hideFrom = rowLines[2];
    var hideTo = rowLines[^2];
    var kept = lines.Take(hideFrom)
        .Append($"-- ... {hideTo - hideFrom + 1} more rows like these ...")
        .Concat(lines.Skip(hideTo + 1));
    return string.Join(Environment.NewLine, kept);
}
