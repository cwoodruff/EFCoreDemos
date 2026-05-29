using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaginationBenchmark.Data;

// Seeds the Orders table with N rows for benchmarking pagination strategies.
// Default: 1,000,000 rows. Override with: dotnet run -- <rowCount>

const string ConnectionString =
    "Server=(localdb)\\MSSQLLocalDB;Database=PaginationBenchmark;Trusted_Connection=True;TrustServerCertificate=True;";

int rowCount = 1_000_000;
if (args.Length > 0 && int.TryParse(args[0], out var parsed))
    rowCount = parsed;

Console.WriteLine($"Seeding {rowCount:N0} rows into PaginationBenchmark.Orders...");

// 1. Ensure database + schema exist via EF Core.
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(ConnectionString)
    .Options;

await using (var db = new AppDbContext(options))
{
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    Console.WriteLine("Schema created.");
}

// 2. Bulk-insert rows with SqlBulkCopy. EF Core SaveChanges is far too slow at this scale.
var sw = Stopwatch.StartNew();
var startDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var rng = new Random(42);

var table = new DataTable();
table.Columns.Add("Id", typeof(int));
table.Columns.Add("CustomerId", typeof(int));
table.Columns.Add("CreatedAt", typeof(DateTime));
table.Columns.Add("Total", typeof(decimal));
table.Columns.Add("Status", typeof(string));

const int BatchSize = 50_000;
var statuses = new[] { "Pending", "Paid", "Shipped", "Delivered", "Cancelled" };

await using var conn = new SqlConnection(ConnectionString);
await conn.OpenAsync();

for (int batchStart = 1; batchStart <= rowCount; batchStart += BatchSize)
{
    table.Rows.Clear();
    int batchEnd = Math.Min(batchStart + BatchSize - 1, rowCount);

    for (int id = batchStart; id <= batchEnd; id++)
    {
        // CreatedAt advances roughly linearly so deeper pages map to later dates.
        var createdAt = startDate.AddSeconds(id * 30 + rng.Next(0, 30));
        var customerId = rng.Next(1, 10_001);
        var total = Math.Round((decimal)(rng.NextDouble() * 1000), 2);
        var status = statuses[rng.Next(statuses.Length)];

        table.Rows.Add(id, customerId, createdAt, total, status);
    }

    // KeepIdentity preserves the Id values from our DataTable instead of letting
    // SQL Server assign new ones from the IDENTITY sequence.
    using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.KeepIdentity, externalTransaction: null)
    {
        DestinationTableName = "Orders",
        BatchSize = BatchSize,
        BulkCopyTimeout = 300,
    };
    bulk.ColumnMappings.Add("Id", "Id");
    bulk.ColumnMappings.Add("CustomerId", "CustomerId");
    bulk.ColumnMappings.Add("CreatedAt", "CreatedAt");
    bulk.ColumnMappings.Add("Total", "Total");
    bulk.ColumnMappings.Add("Status", "Status");

    await bulk.WriteToServerAsync(table);

    Console.WriteLine($"  Inserted {batchEnd:N0} / {rowCount:N0}");
}

sw.Stop();
Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s.");

// 3. Update statistics so the optimizer has accurate cardinality estimates.
using (var cmd = new SqlCommand("UPDATE STATISTICS Orders WITH FULLSCAN;", conn))
{
    cmd.CommandTimeout = 300;
    await cmd.ExecuteNonQueryAsync();
}
Console.WriteLine("Statistics updated.");
