using System.Data;
using bulk_insert_perf.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace bulk_insert_perf;

/// <summary>
/// The same rows inserted five ways. Every method starts from fresh (untracked) entities and a new context,
/// like an import job would.
/// </summary>
public static class BulkInserts
{
    /// <summary>
    /// Rows per command once MaxBatchSize is tuned. OrderLine sends 5 parameters per row and SQL Server allows
    /// 2,100 per command, so 400 rows (2,000 parameters) is about as large as a batch can get.
    /// </summary>
    public const int TunedBatchSize = 400;

    /// <summary>The anti-pattern: Add + SaveChanges per row. One round trip and one transaction per row.</summary>
    public static async Task PerRowSaveChangesAsync(DbContextOptions<BulkContext> options, IEnumerable<OrderLine> rows)
    {
        await using var db = new BulkContext(options);
        foreach (var row in rows)
        {
            db.OrderLines.Add(row);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>AddRange + one SaveChanges: EF splits the rows into batches of MaxBatchSize rows per command.</summary>
    public static async Task AddRangeAsync(DbContextOptions<BulkContext> options, IEnumerable<OrderLine> rows)
    {
        await using var db = new BulkContext(options);
        db.OrderLines.AddRange(rows);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Add in a loop with AutoDetectChangesEnabled = false, then one SaveChanges. The classic EF6 tip; in EF Core
    /// Add doesn't scan the change tracker anyway, so expect little or no difference from AddRange.
    /// </summary>
    public static async Task AddWithoutDetectChangesAsync(DbContextOptions<BulkContext> options, IEnumerable<OrderLine> rows)
    {
        await using var db = new BulkContext(options);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var row in rows)
        {
            db.OrderLines.Add(row);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The escape hatch: SqlBulkCopy streams rows with the TDS bulk-load protocol. No change tracking, no
    /// generated Ids read back, no EF at all.
    /// </summary>
    public static async Task SqlBulkCopyAsync(string connectionString, IReadOnlyCollection<OrderLine> rows)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(OrderLine.OrderNumber), typeof(string));
        table.Columns.Add(nameof(OrderLine.Sku), typeof(string));
        table.Columns.Add(nameof(OrderLine.Quantity), typeof(int));
        table.Columns.Add(nameof(OrderLine.UnitPrice), typeof(decimal));
        table.Columns.Add(nameof(OrderLine.OrderedAt), typeof(DateTime));
        foreach (var row in rows)
        {
            table.Rows.Add(row.OrderNumber, row.Sku, row.Quantity, row.UnitPrice, row.OrderedAt);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var bulkCopy = new SqlBulkCopy(connection)
        {
            DestinationTableName = "dbo.OrderLines",
            BatchSize = 10_000
        };
        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table);
    }
}
